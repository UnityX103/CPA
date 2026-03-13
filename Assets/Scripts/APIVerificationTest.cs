using CPA.Monitoring;
using UnityEngine;

public class APIVerificationTest : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("[APIVerificationTest] 开始 API 验证测试...");
        
        TestGetCurrentApp();
        TestGetAppIcon();
        
        Debug.Log("[APIVerificationTest] API 验证测试完成");
        
        Application.Quit();
    }
    
    private void TestGetCurrentApp()
    {
        Debug.Log("[APIVerificationTest] 测试 GetCurrentApp() API...");
        
        try
        {
            AppInfo result = AppMonitor.Instance.GetCurrentApp();
            
            if (result == null)
            {
                Debug.LogError("[APIVerificationTest] ✗ GetCurrentApp() 返回 null");
                return;
            }
            
            Debug.Log($"[APIVerificationTest] ✓ GetCurrentApp() 成功");
            Debug.Log($"[APIVerificationTest]   IsSuccess: {result.IsSuccess}");
            Debug.Log($"[APIVerificationTest]   AppName: {result.AppName}");
            Debug.Log($"[APIVerificationTest]   WindowTitle: {result.WindowTitle}");
            Debug.Log($"[APIVerificationTest]   Icon: {(result.Icon != null ? $"{result.Icon.width}x{result.Icon.height}" : "null")}");
            
            if (result.ErrorCode.HasValue)
            {
                Debug.Log($"[APIVerificationTest]   ErrorCode: {result.ErrorCode.Value}");
                Debug.Log($"[APIVerificationTest]   ErrorMessage: {result.ErrorMessage}");
            }
            
            if (!result.IsSuccess)
            {
                Debug.LogWarning("[APIVerificationTest] ⚠ API 返回失败状态，但这可能是正常的（权限未授予）");
            }
            
            if (string.IsNullOrWhiteSpace(result.AppName))
            {
                Debug.LogError("[APIVerificationTest] ✗ AppName 为空");
            }
            else
            {
                Debug.Log("[APIVerificationTest] ✓ AppName 有效");
            }
            
            if (result.Icon == null)
            {
                Debug.LogWarning("[APIVerificationTest] ⚠ Icon 为 null");
            }
            else
            {
                Debug.Log("[APIVerificationTest] ✓ Icon 有效");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[APIVerificationTest] ✗ GetCurrentApp() 抛出异常: {ex.Message}");
            Debug.LogError($"[APIVerificationTest]   堆栈: {ex.StackTrace}");
        }
    }
    
    private void TestGetAppIcon()
    {
        Debug.Log("[APIVerificationTest] 测试 GetAppIcon() API...");
        
        try
        {
            Texture2D icon = AppMonitor.Instance.GetAppIcon();
            
            if (icon == null)
            {
                Debug.LogWarning("[APIVerificationTest] ⚠ GetAppIcon() 返回 null");
            }
            else
            {
                Debug.Log($"[APIVerificationTest] ✓ GetAppIcon() 成功: {icon.width}x{icon.height}");
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[APIVerificationTest] ✗ GetAppIcon() 抛出异常: {ex.Message}");
        }
    }
}
