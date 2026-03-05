using System.Collections;
using NUnit.Framework;
using NZ.VisualTest.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace NZ.VisualTest.Tests.Runtime
{
    /// <summary>
    /// 可视化测试示例
    /// 演示：正方形旋转 → 空格键 → 鼠标右键 → 测试结束
    /// </summary>
    [TestFixture]
    public class VisualTestExample : VisualTestBase
    {
        private GameObject _square;

        [UnityTest]
        public IEnumerator Test_RotatingSquare()
        {
            // 1. 创建旋转正方形
            _square = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _square.transform.position = Vector3.zero;
            _square.AddComponent<SquareRotator>();

            // 2. 提示测试开始
            LogInputAction("测试开始：正方形旋转中");

            // 3. 等待 2 秒，观察旋转效果
            yield return new WaitForSeconds(2f);

            // 4. 模拟空格键
            yield return SimulateKey(Key.Space, "空格键");

            yield return new WaitForSeconds(0.3f);

            // 5. 模拟鼠标右键
            yield return SimulateMouseButton(1, "鼠标右键");

            yield return new WaitForSeconds(0.3f);

            // 6. 测试完成
            LogInputAction("测试完成");

            // 7. 清理正方形
            Object.Destroy(_square);
            _square = null;

            yield return null;
        }

        private class SquareRotator : MonoBehaviour
        {
            private void Update()
            {
                transform.Rotate(0f, 0f, 90f * Time.deltaTime);
            }
        }
    }
}
