using System;

[Serializable]
public sealed class RgbdFrameMetadata
{
    public int frame_id;
    public double rgb_timestamp;
    public double depth_timestamp;
    public double confidence_timestamp;
    public double timestamp_difference_ms;
    public double pose_timestamp;
    public int rgb_width;
    public int rgb_height;
    public int rgb_row_stride;
    public int depth_width;
    public int depth_height;
    public int depth_row_stride;
    public int depth_pixel_stride;
    public int confidence_width;
    public int confidence_height;
    public int confidence_row_stride;
    public int confidence_pixel_stride;
    public float fx;
    public float fy;
    public float cx;
    public float cy;
    public bool has_intrinsics;
    public string tracking_state;
    public float[] camera_position;
    public float[] camera_rotation;
    public float[] camera_to_world_matrix;
    public string rgb_file;
    public string depth_file;
    public string confidence_file;
    public string rgb_format;
    public string depth_format;
    public string confidence_format;
    public string depth_unit;
    public bool depth_little_endian;
    public string invalid_depth_policy;
    public string confidence_value_meaning;
    public string image_orientation;
    public string applied_rotation_flip;
}

public sealed class RgbdRecordedFrame
{
    public RgbdFrameMetadata Metadata;
    public byte[] RgbBytes;
    public byte[] DepthBytes;
    public byte[] ConfidenceBytes;
}

public sealed class RgbdSessionMetadata
{
    public string schema_version;
    public string scan_id;
    public string capture_start_time_utc;
    public string dataset_path;
    public string unity_version;
    public string ar_foundation_version;
    public string operating_system;
    public string device_model;
    public string runtime_platform;
    public string depth_provider;
    public float target_frame_rate_hz;
    public double max_rgb_depth_timestamp_difference_ms;
    public int max_write_queue;
    public string rgb_format;
    public string depth_format;
    public string depth_unit;
    public string coordinate_system;
    public string camera_forward_convention;
    public string pose_convention;
    public string matrix_serialization_order;
    public string quaternion_order;
    public string world_scale;
    public string timestamp_policy;
    public RgbdRecorderDiagnostics diagnostics;
}
