#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>
#include <string.h>

extern "C" const char* cpa_native_pick_video_file(void)
{
    __block const char* result = NULL;

    void (^pick)(void) = ^{
        @autoreleasepool {
            NSOpenPanel* panel = [NSOpenPanel openPanel];
            panel.allowsMultipleSelection = NO;
            panel.canChooseDirectories = NO;
            panel.canChooseFiles = YES;
            panel.allowedFileTypes = @[@"mp4", @"mov", @"m4v", @"webm"];
            panel.title = @"选择计时结束播放的视频";

            if ([panel runModal] == NSModalResponseOK) {
                NSURL* url = panel.URLs.firstObject;
                if (url != nil) {
                    NSString* path = url.path;
                    if (path != nil && path.length > 0) {
                        result = strdup([path UTF8String]);
                    }
                }
            }
        }
    };

    if ([NSThread isMainThread]) {
        pick();
    } else {
        dispatch_sync(dispatch_get_main_queue(), pick);
    }

    return result;
}
