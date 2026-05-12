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
                        char *bundleId,
                        int bundleIdLen,
                        unsigned char **iconData,
                        int *iconLen)
{
    @autoreleasepool
    {
        if (appName == NULL || nameLen <= 0 || windowTitle == NULL || titleLen <= 0 || bundleId == NULL || bundleIdLen <= 0 || iconData == NULL || iconLen == NULL)
        {
            return AppMonitorResultCodeInvalidArgument;
        }

        appName[0] = '\0';
        windowTitle[0] = '\0';
        bundleId[0] = '\0';
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
        CopyNSStringToBuffer(frontmostApp.bundleIdentifier ?: @"", bundleId, bundleIdLen);

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

// ════════════════════════════════════════════════════════════════════
// 全局按键监听（NSEvent global + local monitor）
// ────────────────────────────────────────────────────────────────────
// 目的：让 BindingKeyCounterSystem 在 Unity 窗口失焦时仍能统计按键。
// 透明桌宠窗口 + 点击穿透下用户 99% 时间都不会让 Unity 拿到 focus，
// 旧的 Unity Input.GetKeyDown 不会触发，按键计数永远是 0。
//
// 设计：
// 1) 同时装 globalMonitor（其它 app 是 active 时触发）+ localMonitor
//    （Unity 自己 active 时触发）。两者互斥不会双发。
// 2) 事件压入一个 NSLock 保护的循环队列，C# 端按帧 KeyMonitor_Poll
//    drain 出来。队列容量 256 条，溢出丢最早，防失控积累。
// 3) Modifier-only（Shift / Cmd / Option / Ctrl 单独按下）走
//    NSEventTypeFlagsChanged，本期暂不支持，因为项目里没人能绑定纯修饰键。
//
// 队列里的 int 编码：
//   -1 = 鼠标左键   (匹配 BindingKeyModel.MouseLeft)
//   -2 = 鼠标右键   (匹配 BindingKeyModel.MouseRight)
//   -3 = 鼠标中键   (匹配 BindingKeyModel.MouseMiddle)
//   >=0 = macOS CGKeyCode（HIToolbox kVK_*），由 C# 端
//        GlobalKeyMonitor.TranslateMacKeyCode 映射成 UnityEngine.KeyCode int。
// ════════════════════════════════════════════════════════════════════

static id sGlobalKeyMonitor = nil;     // 其它 app active 时触发
static id sLocalKeyMonitor = nil;      // Unity 自己 active 时触发
static NSMutableArray<NSNumber *> *sKeyQueue = nil;
static NSLock *sKeyQueueLock = nil;
static const NSUInteger kMaxKeyQueueLength = 256;

// 修饰键边沿检测：NSEventTypeFlagsChanged 在按下和释放两次都会触发，
// 必须比较前后 modifierFlags，只有"新增置位"才算按下入队。
// 用 ATOMIC 写不严谨，但 NSEvent 回调本来就在主线程上下文（Cocoa runloop 主线程），
// 这个全局变量不跨线程读写。
static NSEventModifierFlags sLastModifierFlags = 0;

static void KeyMonitor_Enqueue(int code)
{
    if (sKeyQueueLock == nil || sKeyQueue == nil) return;
    [sKeyQueueLock lock];
    if (sKeyQueue.count >= kMaxKeyQueueLength)
    {
        // 溢出丢最早；用户失焦几秒后狂按也不会把进程内存撑爆
        [sKeyQueue removeObjectAtIndex:0];
    }
    [sKeyQueue addObject:@(code)];
    [sKeyQueueLock unlock];
}

static void KeyMonitor_HandleEvent(NSEvent *event)
{
    if (event == nil) return;
    switch (event.type)
    {
        case NSEventTypeKeyDown:
            // event.keyCode 已是 CGKeyCode (0..127)
            KeyMonitor_Enqueue((int)event.keyCode);
            break;
        case NSEventTypeFlagsChanged:
        {
            // 修饰键单独按下走这里，event.type=NSEventTypeKeyDown 不会被触发。
            // 上升沿（newly set bits）→ 入队；下降沿（released）→ 忽略。
            // event.keyCode 区分 Left/Right 修饰键（如 0x38=LeftShift, 0x3C=RightShift）。
            NSEventModifierFlags now = event.modifierFlags;
            NSEventModifierFlags justPressed = now & ~sLastModifierFlags;
            sLastModifierFlags = now;
            if (justPressed != 0)
            {
                KeyMonitor_Enqueue((int)event.keyCode);
            }
            break;
        }
        case NSEventTypeLeftMouseDown:
            KeyMonitor_Enqueue(-1);
            break;
        case NSEventTypeRightMouseDown:
            KeyMonitor_Enqueue(-2);
            break;
        case NSEventTypeOtherMouseDown:
            // 中键是 buttonNumber==2；其它 4/5 暂不区分
            if (event.buttonNumber == 2)
            {
                KeyMonitor_Enqueue(-3);
            }
            break;
        default:
            break;
    }
}

__attribute__((visibility("default")))
int KeyMonitor_Start(void)
{
    @autoreleasepool
    {
        if (sGlobalKeyMonitor != nil) return 1; // 已装

        // NSEvent 全局监听器需要辅助功能权限。AXIsProcessTrusted 在某些 macOS 版本上
        // 对 NSEvent 监听器其实不严格匹配（"输入监控"是另一项权限，14+），但 trusted
        // 是必要条件——没有它直接 return 0，让 C# 端降级到只用 Unity Input。
        if (!AXIsProcessTrusted()) return 0;

        if (sKeyQueue == nil) sKeyQueue = [NSMutableArray arrayWithCapacity:kMaxKeyQueueLength];
        if (sKeyQueueLock == nil) sKeyQueueLock = [[NSLock alloc] init];

        NSEventMask mask = NSEventMaskKeyDown
            | NSEventMaskFlagsChanged
            | NSEventMaskLeftMouseDown
            | NSEventMaskRightMouseDown
            | NSEventMaskOtherMouseDown;

        // 启动时同步一次当前修饰键状态，避免"启动瞬间用户正按着 Shift"被算成新按下。
        sLastModifierFlags = [NSEvent modifierFlags];

        sGlobalKeyMonitor = [NSEvent addGlobalMonitorForEventsMatchingMask:mask
                                                                   handler:^(NSEvent * _Nonnull event) {
            KeyMonitor_HandleEvent(event);
        }];

        // localMonitor 的 handler 必须 return event，否则会"吃掉"事件让 Unity 自己拿不到。
        sLocalKeyMonitor = [NSEvent addLocalMonitorForEventsMatchingMask:mask
                                                                  handler:^NSEvent * _Nullable(NSEvent * _Nonnull event) {
            KeyMonitor_HandleEvent(event);
            return event;
        }];

        return (sGlobalKeyMonitor != nil || sLocalKeyMonitor != nil) ? 1 : 0;
    }
}

__attribute__((visibility("default")))
void KeyMonitor_Stop(void)
{
    @autoreleasepool
    {
        if (sGlobalKeyMonitor != nil)
        {
            [NSEvent removeMonitor:sGlobalKeyMonitor];
            sGlobalKeyMonitor = nil;
        }
        if (sLocalKeyMonitor != nil)
        {
            [NSEvent removeMonitor:sLocalKeyMonitor];
            sLocalKeyMonitor = nil;
        }
        if (sKeyQueueLock != nil)
        {
            [sKeyQueueLock lock];
            [sKeyQueue removeAllObjects];
            [sKeyQueueLock unlock];
        }
    }
}

__attribute__((visibility("default")))
int KeyMonitor_IsRunning(void)
{
    return (sGlobalKeyMonitor != nil) ? 1 : 0;
}

__attribute__((visibility("default")))
int KeyMonitor_Poll(int *outBuffer, int bufferCapacity)
{
    if (outBuffer == NULL || bufferCapacity <= 0) return 0;
    if (sKeyQueueLock == nil || sKeyQueue == nil) return 0;

    int copied = 0;
    [sKeyQueueLock lock];
    NSUInteger toCopy = MIN((NSUInteger)bufferCapacity, sKeyQueue.count);
    for (NSUInteger i = 0; i < toCopy; i++)
    {
        outBuffer[i] = [sKeyQueue[i] intValue];
    }
    if (toCopy > 0)
    {
        [sKeyQueue removeObjectsInRange:NSMakeRange(0, toCopy)];
    }
    copied = (int)toCopy;
    [sKeyQueueLock unlock];
    return copied;
}
