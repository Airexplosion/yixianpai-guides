namespace YxGuides
{
    internal static class GuideLocale
    {
        public static bool English
        {
            get
            {
                try
                {
                    string code = I2.Loc.LocalizationManager.CurrentLanguageCode;
                    return code != null && code.Length >= 2 && (code[0] == 'e' || code[0] == 'E') && (code[1] == 'n' || code[1] == 'N');
                }
                catch { return false; }
            }
        }

        public static string T(string zh, string en) { return English ? en : zh; }

        public static string Error(string message)
        {
            if (!English) return message;
            switch (message)
            {
                case "攻略过大": return "Guide is too large";
                case "不支持的攻略格式，请更新 MOD": return "Unsupported guide format; update this MOD";
                case "攻略标识不正确": return "Invalid guide ID";
                case "仙命数量不正确": return "Invalid talent count";
                case "仙命标识不正确": return "Invalid talent ID";
                case "阶段数量不正确": return "Invalid stage count";
                case "阶段格式不正确": return "Invalid stage data";
                case "如果条件不正确": return "Invalid If condition";
                case "阶段规则不正确": return "Invalid stage rule";
                case "阶段 ID 重复": return "Duplicate stage ID";
                case "否则阶段引用不正确": return "Invalid Else stage reference";
                case "卡牌规则不正确": return "Invalid card rule";
                case "此攻略不适用于当前角色": return "This guide is for another character";
                case "此攻略不适用于当前副职": return "This guide is for another side job";
                case "多个阶段优先级相同，请作者调整；暂不提供确定建议": return "Multiple stages tie in priority; ask the author to resolve this conflict";
                case "攻略未覆盖当前阶段": return "This guide does not cover the current stage";
                default: return message;
            }
        }
    }
}
