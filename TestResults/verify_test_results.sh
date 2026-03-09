#!/bin/bash
# verify_test_results.sh - 验证 macOS Player 测试结果

set -e

RESULTS_FILE="/Users/xpy/Desktop/NanZhai/CPA/TestResults/macOS_Player_Results.xml"
LOG_FILE="/Users/xpy/Desktop/NanZhai/CPA/TestResults/player.log"

echo "═══════════════════════════════════════════════════════════════"
echo "           macOS Player 测试结果验证"
echo "═══════════════════════════════════════════════════════════════"
echo ""

# 检查结果文件是否存在
if [ ! -f "$RESULTS_FILE" ]; then
    echo "❌ 错误: 测试结果文件不存在"
    echo "   路径: $RESULTS_FILE"
    echo ""
    echo "请先执行测试："
    echo "  ./Builds/macOS/DevTemplate.app/Contents/MacOS/DevTemplate \\"
    echo "    -testPlatform PlayMode \\"
    echo "    -testResults $RESULTS_FILE \\"
    echo "    -logFile $LOG_FILE"
    exit 1
fi

echo "✓ 测试结果文件存在"
echo ""

# 解析测试统计
TOTAL=$(grep -o 'testcasecount="[0-9]*"' "$RESULTS_FILE" | grep -o '[0-9]*' | head -1)
PASSED=$(grep -c 'result="Passed"' "$RESULTS_FILE" || echo "0")
FAILED=$(grep -c 'result="Failed"' "$RESULTS_FILE" || echo "0")
SKIPPED=$(grep -c 'result="Skipped"' "$RESULTS_FILE" || echo "0")

echo "测试统计："
echo "  总计: $TOTAL"
echo "  通过: $PASSED ✓"
echo "  失败: $FAILED"
echo "  跳过: $SKIPPED"
echo ""

# 验证预期测试数量
EXPECTED_TESTS=4
if [ "$TOTAL" -ne "$EXPECTED_TESTS" ]; then
    echo "⚠️  警告: 测试数量不符合预期"
    echo "   预期: $EXPECTED_TESTS"
    echo "   实际: $TOTAL"
    echo ""
fi

# 检查是否所有测试通过
if [ "$PASSED" -eq "$TOTAL" ] && [ "$FAILED" -eq "0" ]; then
    echo "═══════════════════════════════════════════════════════════════"
    echo "✅ 所有测试通过！($PASSED/$TOTAL)"
    echo "═══════════════════════════════════════════════════════════════"
    echo ""
    
    # 显示测试用例详情
    echo "测试用例详情："
    grep -o 'name="[^"]*".*result="[^"]*"' "$RESULTS_FILE" | while read -r line; do
        TEST_NAME=$(echo "$line" | grep -o 'name="[^"]*"' | cut -d'"' -f2 | sed 's/.*\.//')
        RESULT=$(echo "$line" | grep -o 'result="[^"]*"' | cut -d'"' -f2)
        
        if [ "$RESULT" = "Passed" ]; then
            echo "  ✓ $TEST_NAME"
        else
            echo "  ✗ $TEST_NAME ($RESULT)"
        fi
    done
    echo ""
    
    # 检查日志文件
    if [ -f "$LOG_FILE" ]; then
        echo "日志文件: $LOG_FILE"
        echo "  大小: $(du -h "$LOG_FILE" | cut -f1)"
        
        # 检查是否有错误
        ERROR_COUNT=$(grep -c "Exception\|Error" "$LOG_FILE" || echo "0")
        if [ "$ERROR_COUNT" -gt "0" ]; then
            echo "  ⚠️  发现 $ERROR_COUNT 个错误/异常（可能是预期的）"
        else
            echo "  ✓ 无错误"
        fi
    fi
    echo ""
    
    exit 0
else
    echo "═══════════════════════════════════════════════════════════════"
    echo "❌ 测试失败！"
    echo "═══════════════════════════════════════════════════════════════"
    echo ""
    
    # 显示失败的测试
    echo "失败的测试："
    grep 'result="Failed"' "$RESULTS_FILE" | grep -o 'name="[^"]*"' | cut -d'"' -f2 | while read -r test; do
        echo "  ✗ $test"
    done
    echo ""
    
    # 显示日志文件位置
    if [ -f "$LOG_FILE" ]; then
        echo "查看详细日志："
        echo "  cat $LOG_FILE"
        echo ""
        
        # 显示最后 20 行日志
        echo "最后 20 行日志："
        echo "---"
        tail -20 "$LOG_FILE"
        echo "---"
    fi
    
    exit 1
fi
