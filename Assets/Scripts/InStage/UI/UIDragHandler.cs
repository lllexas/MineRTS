using UnityEngine;
using UnityEngine.EventSystems;

public class UIDragHandler : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform _rectTransform;
    private Vector2 _offset;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        // 当鼠标按下时，计算鼠标位置与窗口左下角的偏移量
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out _offset
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        // 当鼠标拖拽时，根据鼠标当前位置和之前算好的偏移量，来更新窗口位置
        if (_rectTransform == null) return;

        Vector2 localPointerPosition;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_rectTransform.parent, // 在父级坐标系下计算
            eventData.position,
            eventData.pressEventCamera,
            out localPointerPosition
        ))
        {
            _rectTransform.localPosition = localPointerPosition - _offset;
        }
    }
}