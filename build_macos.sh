#!/usr/bin/env bash
set -euo pipefail

# macOS 应用自动打包脚本
# 功能：构建、签名、验证 Unity macOS 应用

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="${SCRIPT_DIR}"
UNITY_PATH="/Applications/Unity6/Unity/Unity.app/Contents/MacOS/Unity"
BUILD_OUTPUT="${PROJECT_ROOT}/Builds/macOS/CTClock.app"
LOG_FILE="${PROJECT_ROOT}/build.log"

echo "========================================="
echo "macOS 应用自动打包脚本"
echo "========================================="
echo "项目路径: ${PROJECT_ROOT}"
echo "Unity 路径: ${UNITY_PATH}"
echo "输出路径: ${BUILD_OUTPUT}"
echo ""

# 检查 Unity 是否存在
if [ ! -f "${UNITY_PATH}" ]; then
    echo "✗ 错误: 未找到 Unity，请检查路径"
    exit 1
fi

# 清理旧的构建
if [ -d "${BUILD_OUTPUT}" ]; then
    echo "清理旧的构建..."
    rm -rf "${BUILD_OUTPUT}"
fi

# 执行 Unity 构建
echo "开始构建..."
"${UNITY_PATH}" \
    -quit \
    -batchmode \
    -nographics \
    -projectPath "${PROJECT_ROOT}" \
    -executeMethod BuildScript.BuildRunAndVerifyMacOS \
    -logFile "${LOG_FILE}"

BUILD_EXIT_CODE=$?

# 检查构建日志
if [ -f "${LOG_FILE}" ]; then
    echo ""
    echo "========================================="
    echo "构建日志摘要"
    echo "========================================="
    
    # 显示关键日志
    grep -i "\[BuildScript\]" "${LOG_FILE}" || true
    
    # 检查错误
    if grep -qi "error" "${LOG_FILE}"; then
        echo ""
        echo "⚠ 发现错误，完整日志: ${LOG_FILE}"
    fi
fi

# 检查构建结果
if [ ${BUILD_EXIT_CODE} -ne 0 ]; then
    echo ""
    echo "✗ 构建失败 (退出码: ${BUILD_EXIT_CODE})"
    echo "查看完整日志: ${LOG_FILE}"
    exit 1
fi

# 验证构建产物
if [ ! -d "${BUILD_OUTPUT}" ]; then
    echo ""
    echo "✗ 构建失败: 未找到输出文件"
    exit 1
fi

echo ""
echo "========================================="
echo "✓ 构建成功！"
echo "========================================="
echo "应用路径: ${BUILD_OUTPUT}"

# 显示应用信息
APP_SIZE=$(du -sh "${BUILD_OUTPUT}" | cut -f1)
echo "应用大小: ${APP_SIZE}"

# 验证签名
echo ""
echo "验证签名..."
codesign -dv "${BUILD_OUTPUT}" 2>&1 | head -5

# 验证 Entitlements
echo ""
echo "验证 Entitlements..."
if codesign -d --entitlements - "${BUILD_OUTPUT}" 2>&1 | grep -q "com.apple.security.automation.apple-events"; then
    echo "✓ Apple Events 权限已配置"
else
    echo "⚠ Apple Events 权限未找到"
fi

# 验证 Info.plist
echo ""
echo "验证 Info.plist..."
INFO_PLIST="${BUILD_OUTPUT}/Contents/Info.plist"
if plutil -p "${INFO_PLIST}" | grep -q "NSAccessibilityUsageDescription"; then
    echo "✓ Accessibility 权限描述已配置"
else
    echo "⚠ Accessibility 权限描述未找到"
fi

echo ""
echo "========================================="
echo "打包完成！"
echo "========================================="
echo ""
echo "下一步："
echo "1. 运行应用: open \"${BUILD_OUTPUT}\""
echo "2. 首次运行时，前往 系统设置 > 隐私与安全 > 辅助功能"
echo "3. 将 AppMonitor 添加到允许列表"
echo ""
echo "构建日志: ${LOG_FILE}"
