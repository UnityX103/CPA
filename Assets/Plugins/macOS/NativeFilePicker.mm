#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#import <CoreGraphics/CoreGraphics.h>
#import <ApplicationServices/ApplicationServices.h>
#include <string.h>

extern "C" const char* cpa_native_pick_video_file(void)
{
    __block const char* result = NULL;

    void (^pick)(void) = ^{
        @autoreleasepool {
            NSLog(@"[CPA.NativeFilePicker] enter pick block on main thread");

            NSOpenPanel* panel = [NSOpenPanel openPanel];
            panel.allowsMultipleSelection = NO;
            panel.canChooseDirectories = NO;
            panel.canChooseFiles = YES;
            panel.allowedFileTypes = @[@"mp4", @"mov", @"m4v", @"webm"];
            panel.title = @"选择计时结束播放的视频";
            [NSApp activateIgnoringOtherApps:YES];

            NSInteger panelLevel = (NSInteger)CGShieldingWindowLevel();
            if (panelLevel <= NSStatusWindowLevel) {
                panelLevel = NSStatusWindowLevel + 1;
            }
            panel.level = panelLevel;
            [panel orderFrontRegardless];
            NSLog(@"[CPA.NativeFilePicker] panel configured, level=%ld, calling runModal", (long)panelLevel);

            NSModalResponse response = [panel runModal];
            NSLog(@"[CPA.NativeFilePicker] runModal returned response=%ld", (long)response);

            if (response == NSModalResponseOK) {
                NSURL* url = panel.URLs.firstObject;
                if (url != nil) {
                    NSString* path = url.path;
                    if (path != nil && path.length > 0) {
                        result = strdup([path UTF8String]);
                        NSLog(@"[CPA.NativeFilePicker] selected path='%@'", path);
                    } else {
                        NSLog(@"[CPA.NativeFilePicker] OK pressed but path is nil/empty");
                    }
                } else {
                    NSLog(@"[CPA.NativeFilePicker] OK pressed but URLs.firstObject is nil");
                }
            } else {
                NSLog(@"[CPA.NativeFilePicker] modal cancelled or non-OK response");
            }
        }
    };

    if ([NSThread isMainThread]) {
        NSLog(@"[CPA.NativeFilePicker] cpa_native_pick_video_file called on main thread, invoking inline");
        pick();
    } else {
        NSLog(@"[CPA.NativeFilePicker] cpa_native_pick_video_file called off main thread, dispatch_sync to main");
        dispatch_sync(dispatch_get_main_queue(), pick);
    }

    NSLog(@"[CPA.NativeFilePicker] cpa_native_pick_video_file returning %s",
          result != NULL ? "non-null" : "NULL");
    return result;
}

// 取前台应用主窗口所在的 NSScreen 索引（跟 [NSScreen screens] 顺序对齐，
// 也即 macOS 上 CGGetActiveDisplayList / UniWinCore.GetMonitorRectangle 的常见枚举顺序）。
// 返回 -1：未授权、无前台 app、无可用焦点窗口、找不到对应屏幕、或前台 app 是我们自己。
// 需要 Accessibility 授权（与 AppMonitor 共用）。
extern "C" int cpa_native_get_frontmost_window_screen_index(void)
{
    __block int resultIndex = -1;

    void (^query)(void) = ^{
        @autoreleasepool {
            NSRunningApplication* frontApp = [NSWorkspace sharedWorkspace].frontmostApplication;
            if (frontApp == nil) {
                NSLog(@"[CPA.NativeFilePicker] frontmostApplication=nil");
                return;
            }

            pid_t frontPid = frontApp.processIdentifier;
            pid_t selfPid = [NSRunningApplication currentApplication].processIdentifier;
            if (frontPid == selfPid) {
                // 用户当前焦点就在我们自己身上，不需要切屏
                NSLog(@"[CPA.NativeFilePicker] front app is self, skip");
                return;
            }

            AXUIElementRef axApp = AXUIElementCreateApplication(frontPid);
            if (axApp == NULL) {
                NSLog(@"[CPA.NativeFilePicker] AXUIElementCreateApplication 失败 pid=%d", frontPid);
                return;
            }

            CFTypeRef focusedWindow = NULL;
            AXError err = AXUIElementCopyAttributeValue(
                axApp, kAXFocusedWindowAttribute, &focusedWindow);
            if (err != kAXErrorSuccess || focusedWindow == NULL) {
                // kAXErrorAPIDisabled / kAXErrorNotAuthorized 表示没拿到 Accessibility 授权
                NSLog(@"[CPA.NativeFilePicker] AXFocusedWindow 拿不到 err=%d (pid=%d, app=%@)",
                      (int)err, frontPid, frontApp.bundleIdentifier);
                CFRelease(axApp);
                return;
            }

            CFTypeRef positionVal = NULL;
            CFTypeRef sizeVal = NULL;
            AXUIElementCopyAttributeValue((AXUIElementRef)focusedWindow,
                                          kAXPositionAttribute, &positionVal);
            AXUIElementCopyAttributeValue((AXUIElementRef)focusedWindow,
                                          kAXSizeAttribute, &sizeVal);

            CGPoint pos = CGPointZero;
            CGSize size = CGSizeZero;
            BOOL gotPos = (positionVal != NULL) &&
                AXValueGetValue((AXValueRef)positionVal, kAXValueTypeCGPoint, &pos);
            BOOL gotSize = (sizeVal != NULL) &&
                AXValueGetValue((AXValueRef)sizeVal, kAXValueTypeCGSize, &size);

            if (positionVal) CFRelease(positionVal);
            if (sizeVal) CFRelease(sizeVal);
            CFRelease(focusedWindow);
            CFRelease(axApp);

            if (!gotPos || !gotSize) {
                NSLog(@"[CPA.NativeFilePicker] AX 拿不到 position/size: gotPos=%d gotSize=%d", gotPos, gotSize);
                return;
            }

            // AX 返回的是 Core Graphics 顶左原点坐标（原点在主屏顶部）。
            // NSScreen.frame 用的是底左原点坐标。要在 NSScreen 列表中匹配，
            // 用 CG 顶左坐标的中点跟 NSScreen.frame 转成 CG 顶左后做包含测试最干净。
            CGFloat winCenterX_cg = pos.x + size.width * 0.5;
            CGFloat winCenterY_cg = pos.y + size.height * 0.5;

            NSArray<NSScreen*>* screens = [NSScreen screens];
            if (screens.count == 0) {
                NSLog(@"[CPA.NativeFilePicker] [NSScreen screens] 空");
                return;
            }

            // 主屏（screens[0]）的 frame.origin.y 在 NSScreen 坐标里固定为 0；
            // 其他屏幕的 frame 是相对主屏底左偏移。把每块屏幕的 NSScreen frame 转成
            // CG 顶左 frame：cgY = primaryMaxY - (frame.origin.y + frame.size.height)
            CGFloat primaryMaxY = NSMaxY(screens[0].frame);

            for (NSUInteger i = 0; i < screens.count; i++) {
                NSRect f = screens[i].frame;
                CGFloat cgX = f.origin.x;
                CGFloat cgY = primaryMaxY - (f.origin.y + f.size.height);
                CGFloat cgW = f.size.width;
                CGFloat cgH = f.size.height;

                if (winCenterX_cg >= cgX && winCenterX_cg < cgX + cgW &&
                    winCenterY_cg >= cgY && winCenterY_cg < cgY + cgH) {
                    resultIndex = (int)i;
                    NSLog(@"[CPA.NativeFilePicker] 前台窗口中心(CG顶左) (%.1f,%.1f) 命中 screen[%lu] (cg %.1f,%.1f,%.1f,%.1f) app=%@",
                          winCenterX_cg, winCenterY_cg, (unsigned long)i,
                          cgX, cgY, cgW, cgH, frontApp.bundleIdentifier);
                    return;
                }
            }

            NSLog(@"[CPA.NativeFilePicker] 前台窗口中心(CG顶左) (%.1f,%.1f) 没命中任何 NSScreen，screensCount=%lu",
                  winCenterX_cg, winCenterY_cg, (unsigned long)screens.count);
        }
    };

    if ([NSThread isMainThread]) {
        query();
    } else {
        dispatch_sync(dispatch_get_main_queue(), query);
    }

    return resultIndex;
}
