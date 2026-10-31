using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Lip.Api.Tests;

public class FlowTests : IClassFixture<LipFactory>
{
    private readonly LipFactory _factory;
    public FlowTests(LipFactory factory) => _factory = factory;

    [Fact]
    public async Task Excluded_advocate_cannot_see_walled_matter()
    {
        var client = await ClientFor("usr-ben");
        var matters = await client.GetFromJsonAsync<JsonElement>("/api/v1/matters");
        var text = matters.GetRawText();
        Assert.DoesNotContain("Kabanda", text);

        var hidden = await client.GetAsync("/api/v1/matters/mat-kabanda");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Assigned_advocate_sees_own_matter_and_citation()
    {
        var client = await ClientFor("usr-ada");
        var matters = await client.GetFromJsonAsync<JsonElement>("/api/v1/matters");
        Assert.Contains("Mugisha", matters.GetRawText());
        Assert.DoesNotContain("Kabanda", matters.GetRawText());

        var research = await client.PostAsJsonAsync("/api/v1/research", new { question = "employment notice period", jurisdiction = "Uganda", matterId = "mat-employment" });
        research.EnsureSuccessStatusCode();
        var body = await research.Content.ReadAsStringAsync();
        Assert.Contains("demonstration", body);
        Assert.Contains("Employment Act", body);
    }

    [Fact]
    public async Task Unsupported_question_creates_no_citation()
    {
        var client = await ClientFor("usr-ada");
        var research = await client.PostAsJsonAsync("/api/v1/research", new { question = "quantum procedure on Mars", jurisdiction = "Uganda", matterId = "mat-employment" });
        var body = await research.Content.ReadAsStringAsync();
        Assert.Contains("none", body);
        Assert.DoesNotContain("Cap. 226", body);
    }

    [Fact]
    public async Task Act_command_requires_approval_and_requestor_cannot_self_approve()
    {
        var ada = await ClientFor("usr-ada");
        var run = await ada.PostAsJsonAsync("/api/v1/commands/runs", new { mode = "act", query = "Send the approved update to the client", matterId = "mat-employment" });
        var json = await run.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AWAITING_APPROVAL", json.GetProperty("state").GetString());

        var issue = await ada.PostAsync("/api/v1/documents/doc-1/issue", null);
        Assert.Equal(HttpStatusCode.Conflict, issue.StatusCode);
    }

    [Fact]
    public async Task Duplicate_webhook_returns_the_same_enquiry()
    {
        var client = _factory.CreateClient();
        var payload = new { tenantId = "ten-firm", providerEventId = "evt-dup-1", category = "Employment", summary = "Hello", consentGranted = true };
        var first = await client.PostAsJsonAsync("/api/v1/channels/whatsapp/webhook", payload);
        var second = await client.PostAsJsonAsync("/api/v1/channels/whatsapp/webhook", payload);
        var a = await first.Content.ReadFromJsonAsync<JsonElement>();
        var b = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(a.GetProperty("enquiryId").GetString(), b.GetProperty("enquiryId").GetString());
        Assert.True(b.GetProperty("duplicate").GetBoolean());
    }

    [Fact]
    public async Task Otic_admin_sees_usage_and_no_matter()
    {
        var client = await ClientFor("usr-owen");
        var usage = await client.GetFromJsonAsync<JsonElement>("/api/v1/admin/usage");
        Assert.True(usage.GetProperty("rows").GetArrayLength() >= 1);
        Assert.Equal(0, usage.GetProperty("documentBodies").GetArrayLength());
        var matter = await client.GetAsync("/api/v1/matters/mat-employment");
        Assert.Equal(HttpStatusCode.NotFound, matter.StatusCode);
    }

    [Fact]
    public async Task Meter_totals_match_usage_rows()
    {
        var ada = await ClientFor("usr-ada");
        await ada.PostAsJsonAsync("/api/v1/research", new { question = "employment notice", jurisdiction = "Uganda", matterId = "mat-employment" });
        var metrics = await ada.GetFromJsonAsync<JsonElement>("/api/v1/analytics/metrics");
        Assert.True(metrics.GetProperty("aiUnits").GetInt32() >= 1200);
        Assert.Equal(2500000m, metrics.GetProperty("collectedRevenue").GetDecimal());
        Assert.NotEqual(metrics.GetProperty("collectedRevenue").GetDecimal(), metrics.GetProperty("invoicedAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("outcomeProbability").ValueKind);
    }

    [Fact]
    public async Task Report_shows_collected_cash_apart_from_the_invoice_and_hides_the_walled_audit()
    {
        var ada = await ClientFor("usr-ada");
        var metrics = await ada.GetFromJsonAsync<JsonElement>("/api/v1/analytics/metrics");
        Assert.Equal(2500000m, metrics.GetProperty("collectedRevenue").GetDecimal());
        Assert.Equal(4100000m, metrics.GetProperty("invoicedAmount").GetDecimal());
        Assert.Equal(JsonValueKind.Null, metrics.GetProperty("outcomeProbability").ValueKind);
        Assert.True(metrics.GetProperty("activity").GetArrayLength() <= 6);
        Assert.DoesNotContain("Kabanda", metrics.GetRawText());
        Assert.DoesNotContain("Employment contract draft", metrics.GetRawText());
        Assert.False(metrics.TryGetProperty("documentBodies", out _));

        var owen = await ClientFor("usr-owen");
        var otic = await owen.GetFromJsonAsync<JsonElement>("/api/v1/analytics/metrics");
        Assert.DoesNotContain("Employment contract draft", otic.GetRawText());
        Assert.DoesNotContain("sha256-demo-contract", otic.GetRawText());
        Assert.False(otic.TryGetProperty("documentBodies", out _));
    }

    [Fact]
    public async Task Settings_shows_the_wall_to_an_administrator_and_refuses_an_advocate()
    {
        var tina = await ClientFor("usr-tina");
        var directory = await tina.GetFromJsonAsync<JsonElement>("/api/v1/directory");
        var walls = directory.GetProperty("walls").GetRawText();
        Assert.Contains("Kabanda commercial dispute", walls);
        Assert.Contains("usr-ben", walls);
        Assert.Contains("Prior instruction on the opposing party.", walls);
        var audit = await tina.GetAsync("/api/v1/audit");
        audit.EnsureSuccessStatusCode();
        var auditJson = await audit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(auditJson.GetArrayLength() >= 1);

        var ada = await ClientFor("usr-ada");
        var denied = await ada.GetAsync("/api/v1/audit");
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        var adaDirectory = await ada.GetFromJsonAsync<JsonElement>("/api/v1/directory");
        Assert.DoesNotContain("Kabanda", adaDirectory.GetProperty("walls").GetRawText());
    }

    [Fact]
    public async Task Walled_enquiry_is_hidden_from_inbox_and_client()
    {
        var ben = await ClientFor("usr-ben");
        var inbox = await ben.GetFromJsonAsync<JsonElement>("/api/v1/inbox");
        Assert.DoesNotContain("Kabanda", inbox.GetRawText());
        Assert.DoesNotContain("commercial dispute", inbox.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var client = await ben.GetFromJsonAsync<JsonElement>("/api/v1/clients/cli-chris");
        Assert.DoesNotContain("Kabanda", client.GetRawText());
    }

    [Fact]
    public async Task Client_page_lists_visible_matters_and_omits_the_walled_audit()
    {
        var ada = await ClientFor("usr-ada");
        var page = await ada.GetFromJsonAsync<JsonElement>("/api/v1/clients/cli-chris");
        Assert.Contains("Mugisha employment", page.GetRawText());
        Assert.DoesNotContain("Kabanda", page.GetRawText());
        var timeline = page.GetProperty("timeline").EnumerateArray().Select(row => row.GetProperty("id").GetString()).ToList();
        Assert.Contains("aud-chris", timeline);
        Assert.DoesNotContain("aud-kabanda", timeline);
        Assert.Equal("granted", page.GetProperty("client").GetProperty("consentStatus").GetString());
        var audit = await ada.GetAsync("/api/v1/audit");
        Assert.Equal(HttpStatusCode.Forbidden, audit.StatusCode);

        var ben = await ClientFor("usr-ben");
        var hidden = await ben.GetFromJsonAsync<JsonElement>("/api/v1/clients/cli-chris");
        Assert.DoesNotContain("Kabanda", hidden.GetRawText());
        Assert.Contains("aud-chris", hidden.GetRawText());
        Assert.DoesNotContain("aud-kabanda", hidden.GetRawText());
    }

    [Fact]
    public async Task Connectors_stay_inside_the_tenant()
    {
        var grace = await ClientFor("usr-grace");
        var rows = await grace.GetFromJsonAsync<JsonElement>("/api/v1/connectors");
        Assert.True(rows.GetArrayLength() >= 1);
        foreach (var row in rows.EnumerateArray())
            Assert.Equal("ten-corp", row.GetProperty("tenantId").GetString());
        var matters = await grace.GetFromJsonAsync<JsonElement>("/api/v1/matters");
        Assert.Contains("Data-protection", matters.GetRawText());
    }

    [Fact]
    public async Task Different_person_clears_then_owner_activates_and_approves()
    {
        var ada = await ClientFor("usr-ada");
        var paul = await ClientFor("usr-paul");
        var blocked = await ada.PostAsJsonAsync("/api/v1/matters/mat-conflict/clearance", new { decision = "cleared" });
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var cleared = await paul.PostAsJsonAsync("/api/v1/matters/mat-conflict/clearance", new { decision = "cleared" });
        cleared.EnsureSuccessStatusCode();
        var activated = await ada.PostAsync("/api/v1/matters/mat-conflict/activate", null);
        activated.EnsureSuccessStatusCode();

        var created = await ada.PostAsJsonAsync("/api/v1/documents", new { matterId = "mat-employment", title = "Advice note", body = "Draft advice", malwareClear = true });
        created.EnsureSuccessStatusCode();
        var doc = await created.Content.ReadFromJsonAsync<JsonElement>();
        var documentId = doc.GetProperty("id").GetString();
        var requested = await ada.PostAsync($"/api/v1/documents/{documentId}/request-approval", null);
        requested.EnsureSuccessStatusCode();
        var approval = await requested.Content.ReadFromJsonAsync<JsonElement>();
        var approvalId = approval.GetProperty("id").GetString();
        var self = await ada.PostAsJsonAsync($"/api/v1/approvals/{approvalId}/decisions", new { decision = "approved" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);
        var decision = await paul.PostAsJsonAsync($"/api/v1/approvals/{approvalId}/decisions", new { decision = "approved" });
        decision.EnsureSuccessStatusCode();
        var issued = await ada.PostAsync($"/api/v1/documents/{documentId}/issue", null);
        issued.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Command_thread_restores_and_a_walled_question_is_not_found()
    {
        var ada = await ClientFor("usr-ada");
        var ask = await ada.PostAsJsonAsync("/api/v1/commands/runs", new { mode = "ask", query = "What notice should a fixed-term employment contract state?", matterId = "mat-employment" });
        ask.EnsureSuccessStatusCode();
        var asked = await ask.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId = asked.GetProperty("conversationId").GetString();
        Assert.False(string.IsNullOrEmpty(conversationId));
        Assert.Contains("not a licensed corpus", asked.GetRawText());
        Assert.Contains("Employment Act", asked.GetRawText());

        var miss = await ada.PostAsJsonAsync("/api/v1/commands/runs", new { mode = "ask", query = "quantum procedure on Mars", matterId = "mat-employment", conversationId });
        var missed = await miss.Content.ReadAsStringAsync();
        Assert.Contains("No citation has been created", missed);
        Assert.DoesNotContain("Cap. 226", missed);

        var prepare = await ada.PostAsJsonAsync("/api/v1/commands/runs", new { mode = "prepare", query = "Prepare the employment notice clause", matterId = "mat-employment", conversationId });
        prepare.EnsureSuccessStatusCode();
        var prepared = await prepare.Content.ReadFromJsonAsync<JsonElement>();
        var draftId = prepared.GetProperty("draftDocumentId").GetString();
        Assert.False(string.IsNullOrEmpty(draftId));
        var matter = await ada.GetFromJsonAsync<JsonElement>("/api/v1/matters/mat-employment");
        var draft = matter.GetProperty("documents").EnumerateArray().Single(d => d.GetProperty("id").GetString() == draftId);
        Assert.Equal("draft", draft.GetProperty("approvalState").GetString());

        var act = await ada.PostAsJsonAsync("/api/v1/commands/runs", new { mode = "act", query = "Send the employment update to the client", matterId = "mat-employment", conversationId });
        act.EnsureSuccessStatusCode();
        var acted = await act.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("AWAITING_APPROVAL", acted.GetProperty("state").GetString());
        var approvalId = acted.GetProperty("approvalId").GetString();
        Assert.Contains(approvalId!, acted.GetRawText());
        Assert.Contains("Nothing was published", acted.GetRawText());
        var self = await ada.PostAsJsonAsync($"/api/v1/approvals/{approvalId}/decisions", new { decision = "approved" });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);

        var thread = await ada.GetFromJsonAsync<JsonElement>($"/api/v1/commands/runs/{conversationId}");
        Assert.Equal(4, thread.GetProperty("turns").GetArrayLength());
        Assert.Equal(conversationId, thread.GetProperty("id").GetString());

        var seeded = await ada.GetFromJsonAsync<JsonElement>("/api/v1/commands/runs/run-seed");
        Assert.Contains("not a licensed corpus", seeded.GetRawText());
        var list = await ada.GetFromJsonAsync<JsonElement>("/api/v1/commands/runs");
        Assert.Contains(conversationId!, list.GetRawText());

        var ben = await ClientFor("usr-ben");
        var before = await ben.GetFromJsonAsync<JsonElement>("/api/v1/commands/runs");
        var kabanda = await ben.PostAsJsonAsync("/api/v1/commands/runs", new { mode = "ask", query = "Tell me about Kabanda", matterId = (string?)null });
        var missingMatter = await ben.PostAsJsonAsync("/api/v1/commands/runs", new { mode = "ask", query = "hello", matterId = "mat-does-not-exist" });
        Assert.Equal(HttpStatusCode.NotFound, kabanda.StatusCode);
        Assert.Equal(missingMatter.StatusCode, kabanda.StatusCode);
        var hidden = await ben.GetAsync($"/api/v1/commands/runs/{conversationId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var after = await ben.GetFromJsonAsync<JsonElement>("/api/v1/commands/runs");
        Assert.Equal(before.GetArrayLength(), after.GetArrayLength());
        Assert.DoesNotContain("Kabanda", after.GetRawText());
    }

    [Fact]
    public async Task Inbox_reply_and_handover_stay_on_visible_threads()
    {
        var ada = await ClientFor("usr-ada");
        var created = await ada.PostAsJsonAsync("/api/v1/enquiries", new { channel = "web", category = "Employment", summary = "Intake summary for the warehouse notice", consentStatus = "granted" });
        created.EnsureSuccessStatusCode();
        var createdJson = await created.Content.ReadFromJsonAsync<JsonElement>();
        var conversationId = createdJson.GetProperty("conversationId").GetString();
        var inbox = await ada.GetFromJsonAsync<JsonElement>("/api/v1/inbox");
        var intake = inbox.EnumerateArray().Single(row => row.GetProperty("id").GetString() == conversationId);
        Assert.Equal("Intake summary for the warehouse notice", intake.GetProperty("messages")[0].GetProperty("body").GetString());
        Assert.Equal("web", intake.GetProperty("enquiry").GetProperty("sourceChannel").GetString());

        var reply = await ada.PostAsJsonAsync("/api/v1/inbox/con-1/messages", new { body = "We will review the notice clause." });
        reply.EnsureSuccessStatusCode();
        var handed = await ada.PostAsJsonAsync("/api/v1/inbox/con-1/handover", new { ownerId = "usr-paul", reason = "" });
        handed.EnsureSuccessStatusCode();
        var after = await ada.GetFromJsonAsync<JsonElement>("/api/v1/inbox");
        var thread = after.EnumerateArray().Single(row => row.GetProperty("id").GetString() == "con-1");
        Assert.Equal("whatsapp", thread.GetProperty("enquiry").GetProperty("sourceChannel").GetString());
        Assert.Equal("usr-paul", thread.GetProperty("ownerId").GetString());
        Assert.Equal("assigned", thread.GetProperty("queue").GetString());
        var messages = thread.GetProperty("messages").EnumerateArray().Select(m => m.GetProperty("body").GetString()).ToList();
        var replyAt = messages.FindIndex(body => body == "We will review the notice clause.");
        Assert.True(replyAt >= 0);
        Assert.Equal("Handed to counsel. Automated advice is paused.", messages[^1]);
        Assert.True(replyAt < messages.Count - 1);

        var otherTenant = await ada.PostAsJsonAsync("/api/v1/inbox/con-1/handover", new { ownerId = "usr-grace", reason = "Move this" });
        Assert.Equal(HttpStatusCode.BadRequest, otherTenant.StatusCode);
        var clientOwner = await ada.PostAsJsonAsync("/api/v1/inbox/con-1/handover", new { ownerId = "usr-chris", reason = "Move this" });
        Assert.Equal(HttpStatusCode.BadRequest, clientOwner.StatusCode);

        var grace = await ClientFor("usr-grace");
        var corporate = await grace.GetFromJsonAsync<JsonElement>("/api/v1/inbox");
        Assert.Contains("compliance register", corporate.GetRawText(), StringComparison.OrdinalIgnoreCase);

        var ben = await ClientFor("usr-ben");
        var benInbox = await ben.GetFromJsonAsync<JsonElement>("/api/v1/inbox");
        Assert.DoesNotContain("Kabanda", benInbox.GetRawText());
        Assert.DoesNotContain("commercial dispute", benInbox.GetRawText(), StringComparison.OrdinalIgnoreCase);
        var hidden = await ben.PostAsJsonAsync("/api/v1/inbox/con-2/messages", new { body = "hello" });
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task New_matter_stays_inactive_and_a_hidden_enquiry_is_not_found()
    {
        var ada = await ClientFor("usr-ada");
        var createdEnquiry = await ada.PostAsJsonAsync("/api/v1/enquiries", new { channel = "web", category = "Commercial", summary = "Fresh instruction for a proposed matter", consentStatus = "granted" });
        createdEnquiry.EnsureSuccessStatusCode();
        var enquiryId = (await createdEnquiry.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString();

        var proposed = await ada.PostAsJsonAsync("/api/v1/matters", new { enquiryId, title = "Proposed follow-up", ownerId = "usr-paul", practiceArea = "Commercial" });
        proposed.EnsureSuccessStatusCode();
        var proposedJson = await proposed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("proposed", proposedJson.GetProperty("status").GetString());
        Assert.Equal("usr-paul", proposedJson.GetProperty("ownerId").GetString());

        var check = await ada.PostAsJsonAsync("/api/v1/conflicts/checks", new { enquiryId, counterparty = "Kabanda" });
        check.EnsureSuccessStatusCode();
        var held = await ada.PostAsJsonAsync("/api/v1/matters", new { enquiryId, title = "Held for conflict review", practiceArea = "Commercial" });
        held.EnsureSuccessStatusCode();
        var heldJson = await held.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("conflict_review", heldJson.GetProperty("status").GetString());
        Assert.Equal("pending", heldJson.GetProperty("clearanceState").GetString());
        Assert.NotEqual("active", heldJson.GetProperty("status").GetString());

        var otherTenant = await ada.PostAsJsonAsync("/api/v1/matters", new { enquiryId, title = "Wrong tenant owner", ownerId = "usr-grace", practiceArea = "Commercial" });
        Assert.Equal(HttpStatusCode.BadRequest, otherTenant.StatusCode);

        var ben = await ClientFor("usr-ben");
        var hidden = await ben.PostAsJsonAsync("/api/v1/matters", new { enquiryId = "enq-web-2", title = "Should not open", practiceArea = "Litigation" });
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        var benMatters = await ben.GetFromJsonAsync<JsonElement>("/api/v1/matters");
        Assert.Equal(0, benMatters.GetArrayLength());
        Assert.DoesNotContain("Kabanda", benMatters.GetRawText());
    }

    [Fact]
    public async Task Workbench_cites_a_demonstration_passage_and_hides_another_tenants_matter()
    {
        var ada = await ClientFor("usr-ada");
        var hit = await ada.PostAsJsonAsync("/api/v1/research", new { question = "What notice should a fixed-term employment contract state?", jurisdiction = "Uganda", matterId = "mat-employment" });
        hit.EnsureSuccessStatusCode();
        var hitJson = await hit.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("pending", hitJson.GetProperty("reviewState").GetString());
        Assert.Contains("not a licensed corpus", hitJson.GetRawText());
        Assert.Contains("Employment Act", hitJson.GetRawText());
        Assert.True(hitJson.GetProperty("citations").GetArrayLength() >= 1);

        var miss = await ada.PostAsJsonAsync("/api/v1/research", new { question = "quantum procedure on Mars", jurisdiction = "Uganda", matterId = "mat-employment" });
        miss.EnsureSuccessStatusCode();
        var missJson = await miss.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("none", missJson.GetProperty("evidence").GetString());
        Assert.Equal(0, missJson.GetProperty("citations").GetArrayLength());
        Assert.DoesNotContain("Cap. 226", missJson.GetRawText());

        var notes = await ada.GetFromJsonAsync<JsonElement>("/api/v1/research?matterId=mat-employment");
        Assert.Contains("note-1", notes.GetRawText());
        Assert.True(notes.GetArrayLength() >= 2);

        var grace = await ClientFor("usr-grace");
        var own = await grace.PostAsJsonAsync("/api/v1/research", new { question = "What does the data-protection filing require?", jurisdiction = "Uganda", matterId = "mat-nile" });
        own.EnsureSuccessStatusCode();
        var ownJson = await own.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("none", ownJson.GetProperty("evidence").GetString());
        var firm = await grace.GetAsync("/api/v1/research?matterId=mat-employment");
        Assert.Equal(HttpStatusCode.NotFound, firm.StatusCode);
        var firmPost = await grace.PostAsJsonAsync("/api/v1/research", new { question = "employment notice", jurisdiction = "Uganda", matterId = "mat-employment" });
        Assert.Equal(HttpStatusCode.NotFound, firmPost.StatusCode);
        var graceNotes = await grace.GetFromJsonAsync<JsonElement>("/api/v1/research?matterId=mat-nile");
        Assert.Contains("data-protection filing", graceNotes.GetRawText());
        Assert.DoesNotContain("mat-employment", graceNotes.GetRawText());
    }

    [Fact]
    public async Task Contract_disposition_keeps_the_text_and_hides_another_tenants_review()
    {
        var ada = await ClientFor("usr-ada");
        var saved = await ada.PostAsJsonAsync("/api/v1/contracts/conr-1/dispositions", new { disposition = "accepted", reason = "Notice period is acceptable for this engagement.", datesValidated = true });
        saved.EnsureSuccessStatusCode();
        var savedJson = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("accepted", savedJson.GetProperty("disposition").GetString());
        Assert.Equal("Notice period is acceptable for this engagement.", savedJson.GetProperty("dispositionReason").GetString());
        Assert.True(savedJson.GetProperty("datesValidated").GetBoolean());
        Assert.Equal("Mugisha fixed-term contract", savedJson.GetProperty("title").GetString());
        Assert.Contains("unvalidated", savedJson.GetProperty("extractedDates").GetString());

        var adaRows = await ada.GetFromJsonAsync<JsonElement>("/api/v1/contracts");
        Assert.Contains("Mugisha fixed-term contract", adaRows.GetRawText());
        Assert.DoesNotContain("Processor terms", adaRows.GetRawText());

        var grace = await ClientFor("usr-grace");
        var graceRows = await grace.GetFromJsonAsync<JsonElement>("/api/v1/contracts");
        Assert.Contains("Processor terms", graceRows.GetRawText());
        Assert.DoesNotContain("Mugisha", graceRows.GetRawText());
        var hidden = await grace.PostAsJsonAsync("/api/v1/contracts/conr-1/dispositions", new { disposition = "accepted", reason = "Should not apply.", datesValidated = true });
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }

    [Fact]
    public async Task Hearing_verification_stays_on_a_visible_matter()
    {
        var ada = await ClientFor("usr-ada");
        var calendar = await ada.GetFromJsonAsync<JsonElement>("/api/v1/calendar");
        var text = calendar.GetRawText();
        Assert.Contains("Lab. 8/2026 (demonstration)", text);
        Assert.DoesNotContain("HCCS 14/2026", text);
        Assert.DoesNotContain("outcomeProbability", text);

        var before = calendar.GetProperty("hearings").EnumerateArray().First(h => h.GetProperty("id").GetString() == "lit-emp");
        var hidden = await ada.PostAsync("/api/v1/hearings/lit-1/verify", null);
        var missing = await ada.PostAsync("/api/v1/hearings/lit-missing/verify", null);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await hidden.Content.ReadAsStringAsync());

        var verified = await ada.PostAsync("/api/v1/hearings/lit-emp/verify", null);
        verified.EnsureSuccessStatusCode();
        var saved = await verified.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(saved.GetProperty("verifiedByCounsel").GetBoolean());
        Assert.Equal(before.GetProperty("when").GetString(), saved.GetProperty("when").GetString());
        Assert.Equal(before.GetProperty("forum").GetString(), saved.GetProperty("forum").GetString());
        Assert.Equal(before.GetProperty("sourceNote").GetString(), saved.GetProperty("sourceNote").GetString());
        Assert.Equal("Lab. 8/2026 (demonstration)", saved.GetProperty("caseReference").GetString());
        Assert.False(saved.TryGetProperty("outcomeProbability", out _));

        var again = await ada.GetFromJsonAsync<JsonElement>("/api/v1/calendar");
        var row = again.GetProperty("hearings").EnumerateArray().First(h => h.GetProperty("id").GetString() == "lit-emp");
        Assert.True(row.GetProperty("verifiedByCounsel").GetBoolean());
        Assert.DoesNotContain("HCCS 14/2026", again.GetRawText());
    }

    [Fact]
    public async Task Compliance_status_stays_with_the_source_and_hides_another_tenants_obligation()
    {
        var ada = await ClientFor("usr-ada");
        var adaRows = await ada.GetFromJsonAsync<JsonElement>("/api/v1/compliance");
        Assert.Equal(0, adaRows.GetArrayLength());
        Assert.DoesNotContain("data-protection", adaRows.GetRawText());

        var grace = await ClientFor("usr-grace");
        var before = await grace.GetFromJsonAsync<JsonElement>("/api/v1/compliance");
        Assert.Contains("Annual data-protection filing (demonstration)", before.GetRawText());
        Assert.Contains("src-ug-emp", before.GetRawText());

        var saved = await grace.PostAsJsonAsync("/api/v1/obligations/obl-1/status", new { status = "in_review" });
        saved.EnsureSuccessStatusCode();
        var savedJson = await saved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("in_review", savedJson.GetProperty("status").GetString());
        Assert.Equal("src-ug-emp", savedJson.GetProperty("sourceId").GetString());
        Assert.Equal("Annual data-protection filing (demonstration)", savedJson.GetProperty("title").GetString());

        var hidden = await ada.PostAsJsonAsync("/api/v1/obligations/obl-1/status", new { status = "complete" });
        var missing = await ada.PostAsJsonAsync("/api/v1/obligations/obl-missing/status", new { status = "complete" });
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(await missing.Content.ReadAsStringAsync(), await hidden.Content.ReadAsStringAsync());

        var after = await grace.GetFromJsonAsync<JsonElement>("/api/v1/compliance");
        var row = after.EnumerateArray().First(item => item.GetProperty("id").GetString() == "obl-1");
        Assert.Equal("in_review", row.GetProperty("status").GetString());
        Assert.Equal("src-ug-emp", row.GetProperty("sourceId").GetString());
        var adaAfter = await ada.GetFromJsonAsync<JsonElement>("/api/v1/compliance");
        Assert.Equal(0, adaAfter.GetArrayLength());
    }

    [Fact]
    public async Task Calendar_lists_a_visible_task_and_hearing_without_the_walled_hearing()
    {
        var ada = await ClientFor("usr-ada");
        var calendar = await ada.GetFromJsonAsync<JsonElement>("/api/v1/calendar");
        var text = calendar.GetRawText();
        Assert.Contains("Confirm notice-period deviation with client", text);
        Assert.Contains("Lab. 8/2026 (demonstration)", text);
        Assert.DoesNotContain("HCCS 14/2026", text);

        var task = calendar.GetProperty("tasks").EnumerateArray().First(item => item.GetProperty("id").GetString() == "task-1");
        Assert.False(task.GetProperty("dateVerified").GetBoolean());
        Assert.Equal("mat-employment", task.GetProperty("matterId").GetString());
        Assert.Equal("Africa/Kampala", task.GetProperty("timeZone").GetString());

        var verified = await ada.PostAsync("/api/v1/tasks/task-1/verify", null);
        verified.EnsureSuccessStatusCode();
        var saved = await verified.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(saved.GetProperty("dateVerified").GetBoolean());
        Assert.Equal("verified", saved.GetProperty("status").GetString());

        var again = await ada.GetFromJsonAsync<JsonElement>("/api/v1/calendar");
        var after = again.GetProperty("tasks").EnumerateArray().First(item => item.GetProperty("id").GetString() == "task-1");
        Assert.True(after.GetProperty("dateVerified").GetBoolean());
        Assert.Contains("Lab. 8/2026 (demonstration)", again.GetRawText());
        Assert.DoesNotContain("HCCS 14/2026", again.GetRawText());
    }

    [Fact]
    public async Task Acquisition_counts_first_touch_and_keeps_a_later_visit_on_the_original_channel()
    {
        var ada = await ClientFor("usr-ada");
        var enquiries = await ada.GetFromJsonAsync<JsonElement>("/api/v1/enquiries");
        var rows = enquiries.EnumerateArray().ToList();
        var whatsapp = rows.Count(row => row.GetProperty("sourceChannel").GetString() == "whatsapp");
        var web = rows.Count(row => row.GetProperty("sourceChannel").GetString() == "web");
        Assert.True(whatsapp >= 1);
        Assert.True(web >= 1);
        var employment = rows.First(row => row.GetProperty("id").GetString() == "enq-wa-1");
        Assert.Equal("whatsapp", employment.GetProperty("sourceChannel").GetString());
        Assert.Equal("web", employment.GetProperty("latestChannel").GetString());
        Assert.DoesNotContain("walled commercial dispute", enquiries.GetRawText());

        var metrics = await ada.GetFromJsonAsync<JsonElement>("/api/v1/analytics/metrics");
        Assert.Equal(whatsapp, metrics.GetProperty("whatsappFirstTouch").GetInt32());
        Assert.Equal(web, metrics.GetProperty("webFirstTouch").GetInt32());
        Assert.Equal(rows.Count, metrics.GetProperty("uniqueEnquiries").GetInt32());

        var connectors = await ada.GetFromJsonAsync<JsonElement>("/api/v1/connectors");
        Assert.Contains("demo-adapter", connectors.GetRawText());
        Assert.Contains("unavailable", connectors.GetRawText());
        Assert.DoesNotContain("Corporate intake", connectors.GetRawText());
        foreach (var connector in connectors.EnumerateArray())
            Assert.Contains(connector.GetProperty("status").GetString(), new[] { "demo-adapter", "unavailable" });
    }

    [Fact]
    public async Task Portal_hides_the_walled_dispute()
    {
        var chris = await ClientFor("usr-chris");
        var portal = await chris.GetFromJsonAsync<JsonElement>("/api/v1/portal/matters");
        var text = portal.GetRawText();
        Assert.Contains("employment", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Kabanda", text);
        Assert.DoesNotContain("exposure", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("document", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refused_consent_creates_no_enquiry_and_a_granted_one_keeps_web_origin()
    {
        var ada = await ClientFor("usr-ada");
        var before = await ada.GetFromJsonAsync<JsonElement>("/api/v1/enquiries");
        var refused = await ada.PostAsJsonAsync("/api/v1/enquiries", new { channel = "web", category = "Employment", summary = "This must not be stored.", consentStatus = "refused" });
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        var afterRefusal = await ada.GetFromJsonAsync<JsonElement>("/api/v1/enquiries");
        Assert.Equal(before.GetArrayLength(), afterRefusal.GetArrayLength());
        Assert.DoesNotContain("This must not be stored.", afterRefusal.GetRawText());

        var granted = await ada.PostAsJsonAsync("/api/v1/enquiries", new { channel = "web", category = "Employment", summary = "Please review the demonstration notice clause.", consentStatus = "granted" });
        granted.EnsureSuccessStatusCode();
        var created = await granted.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrEmpty(created.GetProperty("id").GetString()));
        Assert.Equal("web", created.GetProperty("sourceChannel").GetString());
        var inbox = await ada.GetFromJsonAsync<JsonElement>("/api/v1/inbox");
        var thread = inbox.EnumerateArray().Single(row => row.GetProperty("id").GetString() == created.GetProperty("conversationId").GetString());
        Assert.Equal("Please review the demonstration notice clause.", thread.GetProperty("messages")[0].GetProperty("body").GetString());
        Assert.Equal("web", thread.GetProperty("enquiry").GetProperty("sourceChannel").GetString());
    }

    private async Task<HttpClient> ClientFor(string userId)
    {
        var anon = _factory.CreateClient();
        var login = await anon.PostAsJsonAsync("/api/v1/auth/demo", new { userId });
        login.EnsureSuccessStatusCode();
        var body = await login.Content.ReadFromJsonAsync<JsonElement>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", "Demo " + body.GetProperty("token").GetString());
        return client;
    }
}

public sealed class LipFactory : WebApplicationFactory<Program>
{
    private readonly string _db;

    public LipFactory()
    {
        _db = Path.Combine(Path.GetTempPath(), $"lip-test-{Guid.NewGuid():N}.db");
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Lip", $"Data Source={_db}");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Lip"] = $"Data Source={_db}",
                ["DemoAuth:Key"] = "lip-demo-signing-key"
            });
        });
    }
}
