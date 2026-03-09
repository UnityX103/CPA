#!/usr/bin/env python3
"""
测试 macOS 应用监控功能 - 获取当前聚焦应用
"""
import subprocess
import time

# 编译并运行测试程序
test_code = '''
#import <Foundation/Foundation.h>
#import <AppKit/AppKit.h>

int main() {
    @autoreleasepool {
        NSWorkspace *workspace = [NSWorkspace sharedWorkspace];
        NSRunningApplication *frontApp = workspace.frontmostApplication;
        
        if (frontApp) {
            NSString *appName = frontApp.localizedName;
            NSString *bundleID = frontApp.bundleIdentifier;
            
            printf("当前聚焦应用:\\n");
            printf("  名称: %s\\n", [appName UTF8String]);
            printf("  Bundle ID: %s\\n", [bundleID UTF8String]);
            printf("  PID: %d\\n", (int)frontApp.processIdentifier);
        } else {
            printf("未检测到聚焦应用\\n");
        }
    }
    return 0;
}
'''

# 写入临时文件
with open('/tmp/test_app_monitor.m', 'w') as f:
    f.write(test_code)

# 编译
print("编译测试程序...")
compile_result = subprocess.run([
    'clang',
    '-framework', 'Foundation',
    '-framework', 'AppKit',
    '/tmp/test_app_monitor.m',
    '-o', '/tmp/test_app_monitor'
], capture_output=True, text=True)

if compile_result.returncode != 0:
    print(f"编译失败: {compile_result.stderr}")
    exit(1)

print("✅ 编译成功\n")

# 运行测试
print("=" * 60)
print("获取当前聚焦应用信息...")
print("=" * 60)

result = subprocess.run(['/tmp/test_app_monitor'], capture_output=True, text=True)
print(result.stdout)

# 清理
subprocess.run(['rm', '/tmp/test_app_monitor.m', '/tmp/test_app_monitor'])
