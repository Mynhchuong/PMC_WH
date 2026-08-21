namespace PmcWh.Web.Helpers;

/// <summary>
/// Whitelist cột hiển thị ở dropdown "Tìm theo" — dùng chung cho mọi màn có 3 ô tìm kết hợp
/// (_SearchConditions partial). PHẢI khớp đúng các "case" đang được PmcWh.Api
/// MaterialsController.SearchColumnFor() chấp nhận, kẻo chọn 1 đằng tìm ra 1 nẻo.
/// </summary>
public static class SearchFieldsCatalog
{
    public static readonly (string Value, string I18nKey, string Label)[] All =
    {
        ("barcode", "colBarcode", "Barcode"),
        ("dev", "colDev", "Dev"),
        ("po", "colPo", "PO"),
        ("supplier", "colSupplier", "Supplier"),
        ("model", "colModel", "Model"),
        ("season", "colSeason", "Season"),
        ("stage", "colStage", "Stage"),
        ("matldescription", "colMatlDescription", "Mat'l Description"),
        ("colorcode", "colColorCode", "Color Code"),
        ("colorway", "colColorway", "Colorway"),
        ("sizespec", "colSize", "Size"),
        ("category", "colCategory", "Category"),
        ("matltype", "colMatlType", "Mat'l Type"),
        ("pic", "colPic", "PIC"),
        ("remark", "colRemark", "Remark"),
    };
}
