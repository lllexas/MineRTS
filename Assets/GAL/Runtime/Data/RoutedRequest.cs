using System;

namespace GAL
{
    /// <summary>
    /// 泛型路由请求 - 事件传递委托的标准格式
    /// </summary>
    public class RoutedRequest<T> : SpaceTUI.IRoutedRequest
    {
        public string uiid { get; set; }
        public T data { get; set; }
        public Action onComplete { get; set; }
    }
}