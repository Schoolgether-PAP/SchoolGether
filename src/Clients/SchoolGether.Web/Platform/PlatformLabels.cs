namespace SchoolGether.Web.Platform;

public static class PlatformLabels
{
    public static string Action(string code) => UiMessages.Get(code switch
    {
        "institution.list" => "AuditInstitutionList",
        "institution.read" => "AuditInstitutionRead",
        "institution.create" or "institution.created" => "AuditInstitutionCreate",
        "institution.update" => "AuditInstitutionUpdate",
        "institution.suspension" => "AuditInstitutionSuspension",
        "institution.administrator.create" => "AuditResponsibleCreate",
        "institution.account.create" => "AuditAccountCreate",
        "platform.admin.list" => "AuditAdminList",
        "platform.admin.create" => "AuditAdminCreate",
        "platform.admin.update" => "AuditAdminUpdate",
        "platform.audit.list" => "AuditHistoryList",
        "platform.master.promote" => "AuditMasterPromote",
        "platform.dashboard.read" => "AuditDashboardRead",
        "platform.dashboard.preferences.read" => "AuditPreferencesRead",
        "platform.dashboard.preferences.save" => "AuditPreferencesSave",
        "platform.admin.manage" => "AuditAdminManage",
        "platform.access" => "AuditAccess",
        "platform.request" => "AuditRequest",
        _ => "AuditOtherAction"
    });

    public static string Result(string code) => UiMessages.Get(code switch
    {
        "success" => "AuditSuccess",
        "refused" => "AuditRefused",
        _ => "AuditOtherResult"
    });

    public static string Reason(string value)
    {
        var key = "AuditReason" + value;
        var translated = UiMessages.Get(key);
        return translated == key ? value : translated;
    }
}
