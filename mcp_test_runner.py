#!/usr/bin/env python3
"""
Unity MCP 测试运行器
通过 MCP HTTP API 执行完整的测试流程
"""

import requests
import json
import time
import sys

MCP_BASE_URL = "http://localhost:8080"


def call_mcp_tool(tool_name, params=None):
    """调用 MCP 工具"""
    url = f"{MCP_BASE_URL}/tools/call"
    payload = {"name": tool_name, "arguments": params or {}}

    print(f"\n[调用工具] {tool_name}")
    print(f"[参数] {json.dumps(params, indent=2, ensure_ascii=False)}")

    try:
        response = requests.post(url, json=payload, timeout=30)
        response.raise_for_status()
        result = response.json()
        print(f"[结果] {json.dumps(result, indent=2, ensure_ascii=False)[:500]}")
        return result
    except Exception as e:
        print(f"[错误] {e}")
        return None


def read_mcp_resource(uri):
    """读取 MCP 资源"""
    url = f"{MCP_BASE_URL}/resources/read"
    payload = {"uri": uri}

    print(f"\n[读取资源] {uri}")

    try:
        response = requests.post(url, json=payload, timeout=10)
        response.raise_for_status()
        result = response.json()
        print(f"[结果] {json.dumps(result, indent=2, ensure_ascii=False)[:500]}")
        return result
    except Exception as e:
        print(f"[错误] {e}")
        return None


def main():
    print("=" * 60)
    print("Unity MCP 测试运行器")
    print("=" * 60)

    # 1. 验证编辑器状态
    print("\n[步骤 1] 验证编辑器状态")
    state = read_mcp_resource("mcpforunity://editor/state")
    if not state:
        print("❌ 无法读取编辑器状态")
        return 1

    # 2. 刷新项目
    print("\n[步骤 2] 刷新项目")
    refresh_result = call_mcp_tool(
        "refresh_unity",
        {
            "mode": "force",
            "scope": "scripts",
            "compile": "request",
            "wait_for_ready": True,
        },
    )

    if refresh_result:
        print("✅ 项目刷新完成")

    # 等待编译完成
    time.sleep(3)

    # 3. 检查编译错误
    print("\n[步骤 3] 检查编译错误")
    console_result = call_mcp_tool(
        "read_console",
        {"action": "get", "types": ["error"], "count": 10, "format": "detailed"},
    )

    if console_result:
        errors = console_result.get("content", [])
        if errors:
            print(f"⚠️  发现 {len(errors)} 个错误")
        else:
            print("✅ 无编译错误")

    # 4. 运行测试
    print("\n[步骤 4] 运行 PlayMode 测试")
    test_result = call_mcp_tool(
        "run_tests", {"mode": "PlayMode", "test_names": ["AppMonitorVisualTest"]}
    )

    if not test_result:
        print("❌ 测试启动失败")
        return 1

    job_id = test_result.get("job_id")
    if not job_id:
        print("❌ 未获取到 job_id")
        return 1

    print(f"✅ 测试已启动，job_id: {job_id}")

    # 5. 监控测试进度
    print("\n[步骤 5] 监控测试进度")
    max_attempts = 60
    attempt = 0

    while attempt < max_attempts:
        attempt += 1
        print(f"\n[轮询 {attempt}/{max_attempts}] 检查测试状态...")

        job_result = call_mcp_tool(
            "get_test_job",
            {"job_id": job_id, "wait_timeout": 10, "include_failed_tests": True},
        )

        if not job_result:
            print("⚠️  无法获取测试状态")
            time.sleep(5)
            continue

        status = job_result.get("status")
        print(f"[状态] {status}")

        if status in ["completed", "failed"]:
            print(f"\n✅ 测试完成！状态: {status}")

            # 6. 获取测试结果
            print("\n[步骤 6] 测试结果摘要")
            summary = job_result.get("summary", {})
            print(f"总计: {summary.get('total', 0)}")
            print(f"通过: {summary.get('passed', 0)}")
            print(f"失败: {summary.get('failed', 0)}")
            print(f"跳过: {summary.get('skipped', 0)}")

            # 保存结果到文件
            result_file = (
                "/Users/xpy/Desktop/NanZhai/CPA/TestResults/mcp_test_result.json"
            )
            with open(result_file, "w", encoding="utf-8") as f:
                json.dump(job_result, f, indent=2, ensure_ascii=False)
            print(f"\n✅ 结果已保存到: {result_file}")

            return 0 if status == "completed" else 1

        time.sleep(5)

    print("\n❌ 测试超时")
    return 1


if __name__ == "__main__":
    sys.exit(main())
