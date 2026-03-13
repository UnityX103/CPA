#import <AppKit/AppKit.h>
#import <ApplicationServices/ApplicationServices.h>
#import <limits.h>
#import <stdlib.h>
#import <string.h>

typedef NS_ENUM(int, AppMonitorResultCode)
{
    AppMonitorResultCodeSuccess = 0,
    AppMonitorResultCodeInvalidArgument = -1,
    AppMonitorResultCodeAccessibilityDenied = -2,
    AppMonitorResultCodeNoFrontmostApp = -3,
    AppMonitorResultCodeIconAllocationFailed = -4
};

static void CopyNSStringToBuffer(NSString *value, char *destination, int destinationLength)
{
    if (destination == NULL || destinationLength <= 0)
    {
        return;
    }

    destination[0] = '\0';
    NSString *safeValue = value ?: @"";
    const char *utf8Text = [safeValue UTF8String];
    if (utf8Text == NULL)
    {
        return;
    }

    char *duplicated = strdup(utf8Text);
    if (duplicated == NULL)
    {
        return;
    }

    strncpy(destination, duplicated, (size_t)destinationLength - 1);
    destination[destinationLength - 1] = '\0';
    free(duplicated);
}

static NSString *GetFocusedWindowTitle(NSRunningApplication *application)
{
    if (application == nil)
    {
        return @"";
    }

    AXUIElementRef appElement = AXUIElementCreateApplication(application.processIdentifier);
    if (appElement == NULL)
    {
        return @"";
    }

    NSString *windowTitle = @"";
    CFTypeRef focusedWindowValue = NULL;
    AXError focusedWindowError = AXUIElementCopyAttributeValue(appElement, kAXFocusedWindowAttribute, &focusedWindowValue);

    if (focusedWindowError == kAXErrorSuccess && focusedWindowValue != NULL &&
        CFGetTypeID(focusedWindowValue) == AXUIElementGetTypeID())
    {
        AXUIElementRef focusedWindowElement = (AXUIElementRef)focusedWindowValue;
        CFTypeRef titleValue = NULL;
        AXError titleError = AXUIElementCopyAttributeValue(focusedWindowElement, kAXTitleAttribute, &titleValue);

        if (titleError == kAXErrorSuccess && titleValue != NULL)
        {
            if (CFGetTypeID(titleValue) == CFStringGetTypeID())
            {
                windowTitle = [(__bridge NSString *)titleValue copy];
            }
            else
            {
                NSString *describedValue = [(__bridge id)titleValue description];
                windowTitle = describedValue ?: @"";
            }
        }

        if (titleValue != NULL)
        {
            CFRelease(titleValue);
        }
    }

    if (focusedWindowValue != NULL)
    {
        CFRelease(focusedWindowValue);
    }

    CFRelease(appElement);
    return windowTitle;
}

static NSData *GetAppIconPngData(NSRunningApplication *application)
{
    if (application == nil || application.icon == nil)
    {
        return nil;
    }

    NSData *tiffData = [application.icon TIFFRepresentation];
    if (tiffData == nil)
    {
        return nil;
    }

    NSBitmapImageRep *bitmapRep = [NSBitmapImageRep imageRepWithData:tiffData];
    if (bitmapRep == nil)
    {
        return nil;
    }

    return [bitmapRep representationUsingType:NSBitmapImageFileTypePNG properties:@{}];
}

// 一次性触发系统权限弹窗，应在启动时调用一次
__attribute__((visibility("default")))
void RequestAccessibilityPermission(void)
{
    @autoreleasepool
    {
        NSDictionary *options = @{(__bridge NSString *)kAXTrustedCheckOptionPrompt : @YES};
        AXIsProcessTrustedWithOptions((__bridge CFDictionaryRef)options);
    }
}

// 查询是否已授权（不弹窗）
__attribute__((visibility("default")))
int IsAccessibilityGranted(void)
{
    return AXIsProcessTrusted() ? 1 : 0;
}

__attribute__((visibility("default")))
int GetFrontmostAppInfo(char *appName,
                        int nameLen,
                        char *windowTitle,
                        int titleLen,
                        unsigned char **iconData,
                        int *iconLen)
{
    @autoreleasepool
    {
        if (appName == NULL || nameLen <= 0 || windowTitle == NULL || titleLen <= 0 || iconData == NULL || iconLen == NULL)
        {
            return AppMonitorResultCodeInvalidArgument;
        }

        appName[0] = '\0';
        windowTitle[0] = '\0';
        *iconData = NULL;
        *iconLen = 0;

        // 不弹窗，只检查当前状态
        if (!AXIsProcessTrusted())
        {
            return AppMonitorResultCodeAccessibilityDenied;
        }

        NSRunningApplication *frontmostApp = NSWorkspace.sharedWorkspace.frontmostApplication;
        if (frontmostApp == nil)
        {
            return AppMonitorResultCodeNoFrontmostApp;
        }

        CopyNSStringToBuffer(frontmostApp.localizedName, appName, nameLen);
        CopyNSStringToBuffer(GetFocusedWindowTitle(frontmostApp), windowTitle, titleLen);

        NSData *pngData = GetAppIconPngData(frontmostApp);
        if (pngData != nil && pngData.length > 0)
        {
            if (pngData.length > INT_MAX)
            {
                return AppMonitorResultCodeIconAllocationFailed;
            }

            unsigned char *allocatedData = (unsigned char *)malloc((size_t)pngData.length);
            if (allocatedData == NULL)
            {
                return AppMonitorResultCodeIconAllocationFailed;
            }

            memcpy(allocatedData, pngData.bytes, (size_t)pngData.length);
            *iconData = allocatedData;
            *iconLen = (int)pngData.length;
        }

        return AppMonitorResultCodeSuccess;
    }
}

__attribute__((visibility("default")))
void FreeIconData(unsigned char *data)
{
    if (data != NULL)
    {
        free(data);
    }
}
