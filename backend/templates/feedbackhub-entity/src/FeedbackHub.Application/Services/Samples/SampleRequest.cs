using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Samples;

public sealed record SampleRequest([Required] string Name);
