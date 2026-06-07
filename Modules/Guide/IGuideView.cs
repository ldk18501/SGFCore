namespace GameFramework.Core
{
    public interface IGuideView
    {
        bool IsShowing { get; }

        void Show(GuideViewContext context);
        void RefreshTarget(GuideViewContext context);
        void Hide();
    }
}
