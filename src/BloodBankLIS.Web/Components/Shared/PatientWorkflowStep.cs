namespace BloodBankLIS.Web.Components.Shared;

public sealed record PatientWorkflowStep(
    string Key,
    int Number,
    string Label,
    bool Complete,
    bool Current,
    string Detail);
