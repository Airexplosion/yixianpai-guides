using System.Collections.Generic;
using System.Text;
using Yx.Shared;

namespace YxGuides
{
    internal static class GuideSubscriptions
    {
        public static bool TryPrepare(List<GuideBook> current, GuideBook incoming, out List<object> next)
        {
            next = null;
            if (incoming == null) return false;
            var proposed = new List<object>();
            bool replaced = false;
            for (int i = 0; i < current.Count; i++)
            {
                if (current[i].Id == incoming.Id) { proposed.Add(incoming.Raw); replaced = true; }
                else proposed.Add(current[i].Raw);
            }
            if (!replaced) proposed.Add(incoming.Raw);
            if (proposed.Count > 24 || Encoding.UTF8.GetByteCount(Json.Serialize(proposed)) > 150 * 1024) return false;
            next = proposed;
            return true;
        }
    }
}
