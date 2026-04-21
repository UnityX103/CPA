using System;
using UnityEngine;

namespace CPA.Monitoring
{
    public enum AppMonitorResultCode
    {
        Success = 0,
        InvalidArgument = -1,
        AccessibilityDenied = -2,
        NoFrontmostApp = -3,
        IconAllocationFailed = -4
    }

    public class PermissionDeniedException : Exception
    {
        public PermissionDeniedException(string message) : base(message) { }
        public PermissionDeniedException(string message, Exception innerException) : base(message, innerException) { }
    }

    public class AppInfo
    {
        public string AppName;
        public string BundleId;
        public string WindowTitle;
        public Texture2D Icon;
        public bool IsSuccess;
        public AppMonitorResultCode? ErrorCode;
        public string ErrorMessage;
    }
}
