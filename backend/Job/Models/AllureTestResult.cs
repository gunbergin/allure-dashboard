namespace Job.Models;

using System.Collections.Generic;
using Newtonsoft.Json;

/// <summary>
/// Represents an Allure test result from generated JSON
/// </summary>
public class AllureTestResult
{
    [JsonProperty("uuid")]
    public string? Uuid { get; set; }

    [JsonProperty("historyId")]
    public string? HistoryId { get; set; }

    [JsonProperty("testCaseId")]
    public string? TestCaseId { get; set; }

    [JsonProperty("titlePath")]
    public List<string>? TitlePath { get; set; }

    [JsonProperty("fullName")]
    public string? FullName { get; set; }

    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("statusDetails")]
    public StatusDetails? StatusDetails { get; set; }

    [JsonProperty("labels")]
    public List<Label>? Labels { get; set; }

    [JsonProperty("steps")]
    public List<AllureStep>? Steps { get; set; }

    [JsonProperty("attachments")]
    public List<AllureAttachment>? Attachments { get; set; }

    [JsonProperty("parameters")]
    public List<AllureParameter>? Parameters { get; set; }

    [JsonProperty("start")]
    public long Start { get; set; }

    [JsonProperty("stop")]
    public long Stop { get; set; }

    [JsonProperty("stage")]
    public string? Stage { get; set; }

    [JsonProperty("links")]
    public List<object>? Links { get; set; }
}

public class StatusDetails
{
    [JsonProperty("known")]
    public bool Known { get; set; }

    [JsonProperty("muted")]
    public bool Muted { get; set; }

    [JsonProperty("flaky")]
    public bool Flaky { get; set; }

    [JsonProperty("message")]
    public string? Message { get; set; }

    [JsonProperty("trace")]
    public string? Trace { get; set; }
}

public class Label
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("value")]
    public string? Value { get; set; }
}

public class AllureStep
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("status")]
    public string? Status { get; set; }

    [JsonProperty("statusDetails")]
    public StatusDetails? StatusDetails { get; set; }

    [JsonProperty("stage")]
    public string? Stage { get; set; }

    [JsonProperty("start")]
    public long Start { get; set; }

    [JsonProperty("stop")]
    public long Stop { get; set; }

    [JsonProperty("steps")]
    public List<AllureStep>? Steps { get; set; }

    [JsonProperty("attachments")]
    public List<AllureAttachment>? Attachments { get; set; }

    [JsonProperty("parameters")]
    public List<AllureParameter>? Parameters { get; set; }
}

public class AllureAttachment
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("source")]
    public string? Source { get; set; }

    [JsonProperty("type")]
    public string? Type { get; set; }
}

public class AllureParameter
{
    [JsonProperty("name")]
    public string? Name { get; set; }

    [JsonProperty("value")]
    public string? Value { get; set; }
}
