namespace FeeQuery.Shared.Models;

/// <summary>
/// 云厂商选项 DTO
/// 用于前端下拉选择组件的数据绑定
/// </summary>
public class ProviderOption
{
    /// <summary>
    /// 厂商唯一标识符
    /// </summary>
    public string ProviderCode { get; set; } = "";

    /// <summary>
    /// 厂商显示名称
    /// </summary>
    public string ProviderName { get; set; } = "";

    /// <summary>
    /// 厂商描述
    /// </summary>
    public string Description { get; set; } = "";
}
