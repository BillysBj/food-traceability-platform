namespace FoodTraceability.Modules.Documents.Domain;

public static class StandardDocumentTypeIds
{
    // uuid5(DNS, "food-traceability.documents.document_type.<CODE>"), following D-17.
    public static readonly Guid LabReport = new("5b9827b8-61e4-5046-8787-ff0d1418b4a7");
    public static readonly Guid Certificate = new("b4f4b593-4b19-5f13-8d15-b1d51e126862");
    public static readonly Guid DeliveryNote = new("ae9fd5c5-e18b-5bf4-ac1a-b7ae088e14aa");
}
