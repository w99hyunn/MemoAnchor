using System;

[Serializable]
public sealed class RgbdRecorderDiagnostics
{
    public string recorder_state = "stopped";
    public int captured_frames;
    public int saved_frames;
    public int dropped_frames;
    public int rgb_acquisition_failures;
    public int depth_acquisition_failures;
    public int confidence_acquisition_failures;
    public int intrinsics_failures;
    public int timestamp_rejections;
    public double last_rgb_timestamp;
    public double last_depth_timestamp;
    public double last_timestamp_difference_ms;
    public int last_rgb_width;
    public int last_rgb_height;
    public int last_depth_width;
    public int last_depth_height;
    public string tracking_state = "unknown";
    public int pending_write_queue;
    public string last_error = string.Empty;
    public string dataset_path = string.Empty;

    public RgbdRecorderDiagnostics Clone()
    {
        return (RgbdRecorderDiagnostics)MemberwiseClone();
    }
}
