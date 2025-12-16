using System;
using System.Collections.Generic;

namespace SurvivalcraftTerrainScanner {
    //By Deepseek
    public static class StringExtensions {
        // 定义全角字符的Unicode范围
        static readonly HashSet<int> FullWidthRanges = new() {
            // 罗马数字
            // 0x2160-0x216B, 0x2170-0x217B
            0x2160,
            0x2161,
            0x2162,
            0x2163,
            0x2164,
            0x2165,
            0x2166,
            0x2167,
            0x2168,
            0x2169,
            0x216A,
            0x216B,
            0x2170,
            0x2171,
            0x2172,
            0x2173,
            0x2174,
            0x2175,
            0x2176,
            0x2177,
            0x2178,
            0x2179,
            0x217A,
            0x217B,

            // 中文标点等：0x3000-0x3002, 0x3007-0x3012, 0x30FB
            0x3000,
            0x3001,
            0x3002,
            0x3007,
            0x3008,
            0x3009,
            0x300A,
            0x300B,
            0x300C,
            0x300D,
            0x300E,
            0x300F,
            0x3010,
            0x3011,
            0x3012,
            0x30FB,

            // 注音符号：0x3105-0x312F
            0x3105,
            0x3106,
            0x3107,
            0x3108,
            0x3109,
            0x310A,
            0x310B,
            0x310C,
            0x310D,
            0x310E,
            0x310F,
            0x3110,
            0x3111,
            0x3112,
            0x3113,
            0x3114,
            0x3115,
            0x3116,
            0x3117,
            0x3118,
            0x3119,
            0x311A,
            0x311B,
            0x311C,
            0x311D,
            0x311E,
            0x311F,
            0x3120,
            0x3121,
            0x3122,
            0x3123,
            0x3124,
            0x3125,
            0x3126,
            0x3127,
            0x3128,
            0x3129,
            0x312A,
            0x312B,
            0x312C,
            0x312D,
            0x312E,
            0x312F,

            // 日文部分：0x3041-0x3100 (使用范围判断更高效)

            // 中文部分：0x4E00-0x9FFF (使用范围判断更高效)

            // 全角符号
            0xFF01,
            0xFF08,
            0xFF09,
            0xFF0C,
            0xFF0E,
            0xFF1A,
            0xFF1B,
            0xFF1F,
            0xFF3B,
            0xFF3D,
            0xFF5B,
            0xFF5C,
            0xFF5D,
            0xFF5E,
            0xFFE0,
            0xFFE1,
            0xFFE5,
            0xFFEE
        };

        // 检查字符是否在范围内的高效方法
        static bool IsInRange(int codePoint, int start, int end) => codePoint >= start && codePoint <= end;

        // 判断字符是否为全角字符（宽度为2）
        static bool IsFullWidthChar(char c) {
            int codePoint = c;

            // 检查是否在固定集合中
            if (FullWidthRanges.Contains(codePoint)) {
                return true;
            }

            // 检查日文范围：0x3041-0x3100
            if (IsInRange(codePoint, 0x3041, 0x3100)) {
                return true;
            }

            // 检查中文范围：0x4E00-0x9FFF
            if (IsInRange(codePoint, 0x4E00, 0x9FFF)) {
                return true;
            }

            // 检查全角符号范围：0xFF5B-0xFF5E
            if (IsInRange(codePoint, 0xFF5B, 0xFF5E)) {
                return true;
            }
            return false;
        }

        // 计算字符串的显示宽度（全角字符=2，半角字符=1）
        public static int GetMonoWidth(this string str) {
            if (string.IsNullOrEmpty(str)) {
                return 0;
            }
            int width = 0;
            foreach (char c in str) {
                width += IsFullWidthChar(c) ? 2 : 1;
            }
            return width;
        }

        // 左侧填充（全角字符感知）
        public static string PadLeftMono(this string str, int totalWidth, char paddingChar = ' ') {
            if (str == null) {
                throw new ArgumentNullException(nameof(str));
            }
            int currentWidth = str.GetMonoWidth();
            if (currentWidth >= totalWidth) {
                return str;
            }
            int paddingCount = totalWidth - currentWidth;
            return new string(paddingChar, paddingCount) + str;
        }

        // 右侧填充（全角字符感知）
        public static string PadRightMono(this string str, int totalWidth, char paddingChar = ' ') {
            if (str == null) {
                throw new ArgumentNullException(nameof(str));
            }
            int currentWidth = str.GetMonoWidth();
            if (currentWidth >= totalWidth) {
                return str;
            }
            int paddingCount = totalWidth - currentWidth;
            return str + new string(paddingChar, paddingCount);
        }

        // 居中填充（全角字符感知）
        public static string PadCenterMono(this string str, int totalWidth, char paddingChar = ' ') {
            if (str == null) {
                throw new ArgumentNullException(nameof(str));
            }
            int currentWidth = str.GetMonoWidth();
            if (currentWidth >= totalWidth) {
                return str;
            }
            int paddingCount = totalWidth - currentWidth;
            int leftPadding = paddingCount / 2;
            int rightPadding = paddingCount - leftPadding;
            return new string(paddingChar, leftPadding) + str + new string(paddingChar, rightPadding);
        }

        // 可选：提供一个更高效的判断方法，使用预先计算的哈希集合
        static readonly HashSet<int> FullWidthSet = BuildFullWidthSet();

        static HashSet<int> BuildFullWidthSet() {
            HashSet<int> set = new();

            // 添加单个字符
            foreach (int code in FullWidthRanges) {
                set.Add(code);
            }

            // 添加范围
            AddRange(set, 0x3041, 0x3100); // 日文
            AddRange(set, 0x4E00, 0x9FFF); // 中文
            AddRange(set, 0xFF5B, 0xFF5E); // 全角符号范围
            return set;
        }

        static void AddRange(HashSet<int> set, int start, int end) {
            for (int i = start; i <= end; i++) {
                set.Add(i);
            }
        }

        // 使用哈希集合的优化版本
        static bool IsFullWidthCharOptimized(char c) => FullWidthSet.Contains(c);

        // 使用优化版本的宽度计算方法
        public static int GetMonoWidthOptimized(this string str) {
            if (string.IsNullOrEmpty(str)) {
                return 0;
            }
            int width = 0;
            foreach (char c in str) {
                width += FullWidthSet.Contains(c) ? 2 : 1;
            }
            return width;
        }
    }
}