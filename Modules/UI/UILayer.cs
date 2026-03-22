using UnityEngine;

namespace GameFramework.Core
{
    /// <summary>
    /// UI 渲染层级定义
    /// </summary>
    public enum UILayer
    {
        // 基础层 (主界面，如摇杆、主城 HUD) - Order: 0
        Background = 0,    
        
        // 普通层 (全屏界面，如背包、角色面板) - Order: 1000
        Common = 1,        
        
        // 弹窗层 (对话框、确认框、获得物品展示) - Order: 2000
        Popup = 2,         
        
        // 顶层 (跑马灯公告、浮动提示飘字) - Order: 3000
        Top = 3,           
        
        // 引导层 (新手引导遮罩、强引导手指) - Order: 4000
        Guide = 4,         
        
        // 系统层 (最高优先级：断线重连、防沉迷提示、加载黑屏) - Order: 5000
        System = 5         
    }
}