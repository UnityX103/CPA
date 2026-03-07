#!/usr/bin/env python3
import requests
import json
import time
import sys

MCP_URL = "http://localhost:8080/tools/call"

def call_tool(name, args):
    print(f"\n[调用] {name}")
    resp = requests.post(MCP_URL, json={"name": name, "arguments": args}, timeout=60)
    result = resp.json()
    print(f"[结果] {json.dumps(result, indent=2, ensure_ascii=False)[:300]}")
    return result

# 1. 运行测试
print("=" * 60)
print("启动 PlayMode 测试...")
print("=" * 60)

test_result = call_tool("run_tests", {"mode": "PlayMode"})
job_id = test_result.get("job_id")

if not job_id:
    print("❌ 未获取到 job_id")
    sys.exit(1)

print(f"\n✅ 测试已启动: {job_id}")

# 2. 等待测试完成
print("\n等待测试完成...")
for i in range(60):
    time.sleep(5)
    job_result = call_tool("get_test_job", {
        "job_id": job_id,
        "wait_timeout": 5,
        "include_failed_tests": True
    })
    
    status = job_result.get("status", "unknown")
    print(f"[{i+1}/60] 状态: {status}")
    
    if status in ["completed", "failed"]:
        print(f"\n✅ 测试完成: {status}")
        print(json.dumps(job_result, indent=2, ensure_ascii=False))
        sys.exit(0)

print("\n❌ 测试超时")
sys.exit(1)
