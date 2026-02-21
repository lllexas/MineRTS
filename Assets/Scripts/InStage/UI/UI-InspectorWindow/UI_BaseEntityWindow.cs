using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public abstract class UI_BaseEntityWindow : MonoBehaviour, IPointerDownHandler
{
    public WorkType workType;
    protected EntityHandle targetHandle;

    [Header("基础控件")]
    public TMP_Text titleText;
    public Button closeButton;

    public virtual void Init(EntityHandle handle)
    {
        targetHandle = handle;

        // 【修改】先移除所有旧监听，再添加新监听，防止点一次关十次的惨剧喵！
        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(Close);

        RefreshStaticInfo();
    }

    // 由管理器每帧调用
    public void TickUpdate()
    {
        if (!EntitySystem.Instance.IsValid(targetHandle))
        {
            Close();
            return;
        }
        OnRefresh(EntitySystem.Instance.wholeComponent);
    }

    // 刷新静态信息（如名字、图标，只需切换目标时执行一次）
    protected virtual void RefreshStaticInfo()
    {
        int idx = EntitySystem.Instance.GetIndex(targetHandle);
        string bpName = EntitySystem.Instance.wholeComponent.coreComponent[idx].BlueprintName;
        titleText.text = BlueprintRegistry.Get(bpName).Name;
    }

    // 刷新动态信息（进度条、数字等，每帧执行）
    protected abstract void OnRefresh(WholeComponent whole);

    public void Close()
    {
        // 先通知管理器注销自己
        UI_WindowManager.Instance.UnregisterWindow(workType);

        // 然后自我毁灭
        if (gameObject != null)
        {
            Destroy(gameObject);
        }
    }

    // 点击窗口任何地方，把它提到最前面
    public void OnPointerDown(PointerEventData eventData)
    {
        transform.SetAsLastSibling();
    }
}