using UnityEngine;
using GameFramework.Core.UI;

namespace GameFramework.Core
{
    public class HttpDemo : MonoBehaviour
    {
        public class RankInfo
        {
            public string playerName;
            public int score;
        }

        public class SubmitScoreRequest
        {
            public int myScore;
        }

        public class TestRankPanel : UIFormBase
        {
            public async void FetchRankData()
            {
                // 极其干净的 GET 请求
                var rankData = await GameApp.Http.GetAsync<RankInfo>("https://api.mygame.com/rank/top1");
        
                if (rankData != null)
                {
                    Log.Info($"第一名是：{rankData.playerName}，分数：{rankData.score}");
                }
            }

            public async void SubmitMyScore(int score)
            {
                var req = new SubmitScoreRequest { myScore = score };
        
                // 极其干净的 POST 请求
                var res = await GameApp.Http.PostJsonAsync<SubmitScoreRequest, RankInfo>("https://api.mygame.com/rank/submit", req);
        
                if (res != null)
                {
                    Log.Info("上传分数成功！");
                }
            }
        }
    }
}