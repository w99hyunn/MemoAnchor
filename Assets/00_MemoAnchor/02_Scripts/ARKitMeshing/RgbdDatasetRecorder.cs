using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

public sealed class RgbdDatasetRecorder : IDisposable
{
    private readonly object syncRoot = new object();
    private readonly Queue<RgbdRecordedFrame> pendingFrames = new Queue<RgbdRecordedFrame>();
    private readonly AutoResetEvent workAvailable = new AutoResetEvent(false);
    private Thread workerThread;
    private bool acceptingFrames;
    private bool stopRequested;
    private int maxQueue = 4;
    private RgbdSessionMetadata session;
    private string rgbDirectory;
    private string depthDirectory;
    private string confidenceDirectory;
    private string framesJsonlPath;

    public bool IsRecording { get; private set; }
    public string DatasetPath { get; private set; } = string.Empty;
    public RgbdRecorderDiagnostics Diagnostics { get; } = new RgbdRecorderDiagnostics();

    public void Start(string rootDirectory, RgbdSessionMetadata metadata, int requestedMaxQueue)
    {
        Stop();

        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        maxQueue = Math.Max(1, requestedMaxQueue);
        DatasetPath = Path.Combine(rootDirectory, $"scan_{metadata.scan_id}");
        rgbDirectory = Path.Combine(DatasetPath, "rgb");
        depthDirectory = Path.Combine(DatasetPath, "depth");
        confidenceDirectory = Path.Combine(DatasetPath, "confidence");
        framesJsonlPath = Path.Combine(DatasetPath, "frames.jsonl");

        Directory.CreateDirectory(rgbDirectory);
        Directory.CreateDirectory(depthDirectory);
        Directory.CreateDirectory(confidenceDirectory);
        if (File.Exists(framesJsonlPath))
            File.Delete(framesJsonlPath);

        session = metadata;
        session.dataset_path = DatasetPath;
        session.max_write_queue = maxQueue;
        session.diagnostics = Diagnostics;

        lock (syncRoot)
        {
            pendingFrames.Clear();
            stopRequested = false;
            acceptingFrames = true;
            ResetDiagnosticsLocked();
            Diagnostics.recorder_state = "recording";
            Diagnostics.dataset_path = DatasetPath;
        }

        WriteSessionJson();

        workerThread = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "MemoAnchor RGB-D Recorder"
        };
        workerThread.Start();
        IsRecording = true;
        Debug.Log($"[RGBDRecorder] Started dataset: {DatasetPath}");
    }

    public bool TryEnqueue(RgbdRecordedFrame frame)
    {
        if (frame == null || frame.Metadata == null || frame.RgbBytes == null || frame.DepthBytes == null || frame.ConfidenceBytes == null)
        {
            RecordError("Rejected frame with missing buffers.");
            return false;
        }

        lock (syncRoot)
        {
            if (!acceptingFrames)
                return false;

            while (pendingFrames.Count >= maxQueue)
            {
                pendingFrames.Dequeue();
                Diagnostics.dropped_frames++;
            }

            pendingFrames.Enqueue(frame);
            Diagnostics.captured_frames++;
            Diagnostics.pending_write_queue = pendingFrames.Count;
            Diagnostics.last_rgb_timestamp = frame.Metadata.rgb_timestamp;
            Diagnostics.last_depth_timestamp = frame.Metadata.depth_timestamp;
            Diagnostics.last_timestamp_difference_ms = frame.Metadata.timestamp_difference_ms;
            Diagnostics.last_rgb_width = frame.Metadata.rgb_width;
            Diagnostics.last_rgb_height = frame.Metadata.rgb_height;
            Diagnostics.last_depth_width = frame.Metadata.depth_width;
            Diagnostics.last_depth_height = frame.Metadata.depth_height;
            Diagnostics.tracking_state = frame.Metadata.tracking_state;
        }

        workAvailable.Set();
        return true;
    }

    public void Stop()
    {
        Thread threadToJoin = null;
        lock (syncRoot)
        {
            if (!acceptingFrames && workerThread == null)
                return;

            acceptingFrames = false;
            stopRequested = true;
            Diagnostics.recorder_state = "stopping";
            threadToJoin = workerThread;
        }

        workAvailable.Set();
        if (threadToJoin != null && threadToJoin.IsAlive)
            threadToJoin.Join();

        lock (syncRoot)
        {
            workerThread = null;
            IsRecording = false;
            Diagnostics.pending_write_queue = pendingFrames.Count;
            Diagnostics.recorder_state = "stopped";
        }

        if (!string.IsNullOrWhiteSpace(DatasetPath))
        {
            WriteSessionJson();
            Debug.Log($"[RGBDRecorder] Stopped dataset: {DatasetPath}");
        }
    }

    public void RecordRgbAcquisitionFailure(string error = "")
    {
        IncrementDiagnostic(d => d.rgb_acquisition_failures++, error);
    }

    public void RecordDepthAcquisitionFailure(string error = "")
    {
        IncrementDiagnostic(d => d.depth_acquisition_failures++, error);
    }

    public void RecordConfidenceAcquisitionFailure(string error = "")
    {
        IncrementDiagnostic(d => d.confidence_acquisition_failures++, error);
    }

    public void RecordIntrinsicsFailure(string error = "")
    {
        IncrementDiagnostic(d => d.intrinsics_failures++, error);
    }

    public void RecordTimestampRejection(double deltaMs)
    {
        lock (syncRoot)
        {
            Diagnostics.timestamp_rejections++;
            Diagnostics.last_timestamp_difference_ms = deltaMs;
            Diagnostics.last_error = $"Rejected unsynchronized RGB-D frame: {deltaMs:0.0}ms.";
        }
    }

    public RgbdRecorderDiagnostics SnapshotDiagnostics()
    {
        lock (syncRoot)
        {
            Diagnostics.pending_write_queue = pendingFrames.Count;
            return Diagnostics.Clone();
        }
    }

    public void Dispose()
    {
        Stop();
        workAvailable.Dispose();
    }

    private void WorkerLoop()
    {
        while (true)
        {
            RgbdRecordedFrame frame = null;
            lock (syncRoot)
            {
                if (pendingFrames.Count > 0)
                {
                    frame = pendingFrames.Dequeue();
                    Diagnostics.pending_write_queue = pendingFrames.Count;
                }
                else if (stopRequested)
                {
                    return;
                }
            }

            if (frame == null)
            {
                workAvailable.WaitOne(100);
                continue;
            }

            WriteFrame(frame);
        }
    }

    private void WriteFrame(RgbdRecordedFrame frame)
    {
        try
        {
            var metadata = frame.Metadata;
            File.WriteAllBytes(Path.Combine(DatasetPath, metadata.rgb_file), frame.RgbBytes);
            File.WriteAllBytes(Path.Combine(DatasetPath, metadata.depth_file), frame.DepthBytes);
            File.WriteAllBytes(Path.Combine(DatasetPath, metadata.confidence_file), frame.ConfidenceBytes);

            lock (syncRoot)
            {
                File.AppendAllText(framesJsonlPath, BuildFrameJson(metadata) + "\n", Encoding.UTF8);
                Diagnostics.saved_frames++;
                Diagnostics.pending_write_queue = pendingFrames.Count;
                Diagnostics.last_error = string.Empty;
            }
        }
        catch (Exception ex)
        {
            lock (syncRoot)
            {
                Diagnostics.last_error = ex.Message;
            }
        }
    }

    private void ResetDiagnosticsLocked()
    {
        Diagnostics.captured_frames = 0;
        Diagnostics.saved_frames = 0;
        Diagnostics.dropped_frames = 0;
        Diagnostics.rgb_acquisition_failures = 0;
        Diagnostics.depth_acquisition_failures = 0;
        Diagnostics.confidence_acquisition_failures = 0;
        Diagnostics.intrinsics_failures = 0;
        Diagnostics.timestamp_rejections = 0;
        Diagnostics.last_rgb_timestamp = 0d;
        Diagnostics.last_depth_timestamp = 0d;
        Diagnostics.last_timestamp_difference_ms = 0d;
        Diagnostics.last_rgb_width = 0;
        Diagnostics.last_rgb_height = 0;
        Diagnostics.last_depth_width = 0;
        Diagnostics.last_depth_height = 0;
        Diagnostics.tracking_state = "unknown";
        Diagnostics.pending_write_queue = 0;
        Diagnostics.last_error = string.Empty;
    }

    private void IncrementDiagnostic(Action<RgbdRecorderDiagnostics> increment, string error)
    {
        lock (syncRoot)
        {
            increment(Diagnostics);
            if (!string.IsNullOrWhiteSpace(error))
                Diagnostics.last_error = error;
        }
    }

    private void RecordError(string error)
    {
        lock (syncRoot)
        {
            Diagnostics.last_error = error;
        }
    }

    private void WriteSessionJson()
    {
        if (session == null || string.IsNullOrWhiteSpace(DatasetPath))
            return;

        Directory.CreateDirectory(DatasetPath);
        File.WriteAllText(Path.Combine(DatasetPath, "session.json"), BuildSessionJson(session), Encoding.UTF8);
    }

    private static string BuildSessionJson(RgbdSessionMetadata metadata)
    {
        var json = new StringBuilder(2048);
        json.Append("{\n");
        AppendJson(json, "schema_version", metadata.schema_version, true);
        AppendJson(json, "scan_id", metadata.scan_id, true);
        AppendJson(json, "capture_start_time_utc", metadata.capture_start_time_utc, true);
        AppendJson(json, "dataset_path", metadata.dataset_path, true);
        AppendJson(json, "unity_version", metadata.unity_version, true);
        AppendJson(json, "ar_foundation_version", metadata.ar_foundation_version, true);
        AppendJson(json, "operating_system", metadata.operating_system, true);
        AppendJson(json, "device_model", metadata.device_model, true);
        AppendJson(json, "runtime_platform", metadata.runtime_platform, true);
        AppendJson(json, "depth_provider", metadata.depth_provider, true);
        AppendJson(json, "target_frame_rate_hz", metadata.target_frame_rate_hz, true);
        AppendJson(json, "max_rgb_depth_timestamp_difference_ms", metadata.max_rgb_depth_timestamp_difference_ms, true);
        AppendJson(json, "max_write_queue", metadata.max_write_queue, true);
        AppendJson(json, "rgb_format", metadata.rgb_format, true);
        AppendJson(json, "depth_format", metadata.depth_format, true);
        AppendJson(json, "depth_unit", metadata.depth_unit, true);
        AppendJson(json, "coordinate_system", metadata.coordinate_system, true);
        AppendJson(json, "camera_forward_convention", metadata.camera_forward_convention, true);
        AppendJson(json, "pose_convention", metadata.pose_convention, true);
        AppendJson(json, "matrix_serialization_order", metadata.matrix_serialization_order, true);
        AppendJson(json, "quaternion_order", metadata.quaternion_order, true);
        AppendJson(json, "world_scale", metadata.world_scale, true);
        AppendJson(json, "timestamp_policy", metadata.timestamp_policy, true);
        json.Append("  \"diagnostics\": ");
        AppendDiagnosticsJson(json, metadata.diagnostics);
        json.Append("\n}\n");
        return json.ToString();
    }

    private static string BuildFrameJson(RgbdFrameMetadata metadata)
    {
        var json = new StringBuilder(2048);
        json.Append("{");
        AppendJson(json, "frame_id", metadata.frame_id, false);
        AppendJson(json, "rgb_timestamp", metadata.rgb_timestamp, false);
        AppendJson(json, "depth_timestamp", metadata.depth_timestamp, false);
        AppendJson(json, "confidence_timestamp", metadata.confidence_timestamp, false);
        AppendJson(json, "timestamp_difference_ms", metadata.timestamp_difference_ms, false);
        AppendJson(json, "pose_timestamp", metadata.pose_timestamp, false);
        AppendJson(json, "rgb_width", metadata.rgb_width, false);
        AppendJson(json, "rgb_height", metadata.rgb_height, false);
        AppendJson(json, "rgb_row_stride", metadata.rgb_row_stride, false);
        AppendJson(json, "depth_width", metadata.depth_width, false);
        AppendJson(json, "depth_height", metadata.depth_height, false);
        AppendJson(json, "depth_row_stride", metadata.depth_row_stride, false);
        AppendJson(json, "depth_pixel_stride", metadata.depth_pixel_stride, false);
        AppendJson(json, "confidence_width", metadata.confidence_width, false);
        AppendJson(json, "confidence_height", metadata.confidence_height, false);
        AppendJson(json, "confidence_row_stride", metadata.confidence_row_stride, false);
        AppendJson(json, "confidence_pixel_stride", metadata.confidence_pixel_stride, false);
        AppendJson(json, "fx", metadata.fx, false);
        AppendJson(json, "fy", metadata.fy, false);
        AppendJson(json, "cx", metadata.cx, false);
        AppendJson(json, "cy", metadata.cy, false);
        AppendJson(json, "has_intrinsics", metadata.has_intrinsics, false);
        AppendJson(json, "tracking_state", metadata.tracking_state, false);
        AppendJson(json, "camera_position", metadata.camera_position, false);
        AppendJson(json, "camera_rotation", metadata.camera_rotation, false);
        AppendJson(json, "camera_to_world_matrix", metadata.camera_to_world_matrix, false);
        AppendJson(json, "rgb_file", metadata.rgb_file, false);
        AppendJson(json, "depth_file", metadata.depth_file, false);
        AppendJson(json, "confidence_file", metadata.confidence_file, false);
        AppendJson(json, "rgb_format", metadata.rgb_format, false);
        AppendJson(json, "depth_format", metadata.depth_format, false);
        AppendJson(json, "confidence_format", metadata.confidence_format, false);
        AppendJson(json, "depth_unit", metadata.depth_unit, false);
        AppendJson(json, "depth_little_endian", metadata.depth_little_endian, false);
        AppendJson(json, "invalid_depth_policy", metadata.invalid_depth_policy, false);
        AppendJson(json, "confidence_value_meaning", metadata.confidence_value_meaning, false);
        AppendJson(json, "image_orientation", metadata.image_orientation, false);
        AppendJson(json, "applied_rotation_flip", metadata.applied_rotation_flip, false);
        json.Append("}");
        return json.ToString();
    }

    private static void AppendDiagnosticsJson(StringBuilder json, RgbdRecorderDiagnostics diagnostics)
    {
        if (diagnostics == null)
        {
            json.Append("{}");
            return;
        }

        json.Append("{");
        AppendJson(json, "recorder_state", diagnostics.recorder_state, false);
        AppendJson(json, "captured_frames", diagnostics.captured_frames, false);
        AppendJson(json, "saved_frames", diagnostics.saved_frames, false);
        AppendJson(json, "dropped_frames", diagnostics.dropped_frames, false);
        AppendJson(json, "rgb_acquisition_failures", diagnostics.rgb_acquisition_failures, false);
        AppendJson(json, "depth_acquisition_failures", diagnostics.depth_acquisition_failures, false);
        AppendJson(json, "confidence_acquisition_failures", diagnostics.confidence_acquisition_failures, false);
        AppendJson(json, "intrinsics_failures", diagnostics.intrinsics_failures, false);
        AppendJson(json, "timestamp_rejections", diagnostics.timestamp_rejections, false);
        AppendJson(json, "last_rgb_timestamp", diagnostics.last_rgb_timestamp, false);
        AppendJson(json, "last_depth_timestamp", diagnostics.last_depth_timestamp, false);
        AppendJson(json, "last_timestamp_difference_ms", diagnostics.last_timestamp_difference_ms, false);
        AppendJson(json, "last_rgb_width", diagnostics.last_rgb_width, false);
        AppendJson(json, "last_rgb_height", diagnostics.last_rgb_height, false);
        AppendJson(json, "last_depth_width", diagnostics.last_depth_width, false);
        AppendJson(json, "last_depth_height", diagnostics.last_depth_height, false);
        AppendJson(json, "tracking_state", diagnostics.tracking_state, false);
        AppendJson(json, "pending_write_queue", diagnostics.pending_write_queue, false);
        AppendJson(json, "last_error", diagnostics.last_error, false);
        AppendJson(json, "dataset_path", diagnostics.dataset_path, false);
        json.Append("}");
    }

    private static void AppendJson(StringBuilder json, string name, string value, bool newline)
    {
        if (newline)
            json.Append("  ");
        else if (json[json.Length - 1] != '{')
            json.Append(",");

        json.Append('"').Append(EscapeJson(name)).Append("\":\"").Append(EscapeJson(value ?? string.Empty)).Append('"');
        if (newline)
            json.Append(",\n");
    }

    private static void AppendJson(StringBuilder json, string name, int value, bool newline)
    {
        AppendJsonNumber(json, name, value.ToString(CultureInfo.InvariantCulture), newline);
    }

    private static void AppendJson(StringBuilder json, string name, float value, bool newline)
    {
        AppendJsonNumber(json, name, value.ToString("R", CultureInfo.InvariantCulture), newline);
    }

    private static void AppendJson(StringBuilder json, string name, double value, bool newline)
    {
        AppendJsonNumber(json, name, value.ToString("R", CultureInfo.InvariantCulture), newline);
    }

    private static void AppendJson(StringBuilder json, string name, bool value, bool newline)
    {
        AppendJsonNumber(json, name, value ? "true" : "false", newline);
    }

    private static void AppendJson(StringBuilder json, string name, float[] values, bool newline)
    {
        if (newline)
            json.Append("  ");
        else if (json[json.Length - 1] != '{')
            json.Append(",");

        json.Append('"').Append(EscapeJson(name)).Append("\":[");
        if (values != null)
        {
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0)
                    json.Append(",");
                json.Append(values[i].ToString("R", CultureInfo.InvariantCulture));
            }
        }
        json.Append("]");
        if (newline)
            json.Append(",\n");
    }

    private static void AppendJsonNumber(StringBuilder json, string name, string value, bool newline)
    {
        if (newline)
            json.Append("  ");
        else if (json[json.Length - 1] != '{')
            json.Append(",");

        json.Append('"').Append(EscapeJson(name)).Append("\":").Append(value);
        if (newline)
            json.Append(",\n");
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
}
