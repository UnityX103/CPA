using System;
using System.Collections.Generic;

namespace NZ.VisualTest
{
    /// <summary>
    /// 单次视觉图片测试运行的工件清单。
    /// </summary>
    [Serializable]
    public sealed class VisualImageTestRunManifest
    {
        /// <summary>
        /// 测试方法名。
        /// </summary>
        public string testName;

        /// <summary>
        /// 测试类全名。
        /// </summary>
        public string testClass;

        /// <summary>
        /// 运行实例标识。
        /// </summary>
        public string runId;

        /// <summary>
        /// 运行清单创建时间。
        /// </summary>
        public string createdAt;

        /// <summary>
        /// 当前运行输出目录的绝对路径。
        /// </summary>
        public string outputDirectory;

        /// <summary>
        /// 当前运行中记录的步骤工件。
        /// </summary>
        public List<VisualImageTestStepManifest> steps = new List<VisualImageTestStepManifest>();
    }

    /// <summary>
    /// 单个视觉测试步骤的工件记录。
    /// </summary>
    [Serializable]
    public sealed class VisualImageTestStepManifest
    {
        public int index;
        public string name;

        /// <summary>
        /// 面向当前运行输出目录契约使用的实际图片路径。
        /// </summary>
        public string actualImagePath;

        /// <summary>
        /// 面向当前运行输出目录契约使用的基线图片路径。
        /// </summary>
        public string baselineImagePath;
        public string notes;
    }
}
