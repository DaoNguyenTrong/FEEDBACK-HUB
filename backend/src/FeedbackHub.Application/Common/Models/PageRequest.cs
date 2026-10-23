using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Common.Models;

/// <summary>A page of a <see cref="PagedResult{T}"/> list. Values outside the ranges are rejected with 400, never clamped.</summary>
public sealed record PageRequest(
    [Range(1, int.MaxValue, ErrorMessage = "FieldRange")] int PageNumber = 1,
    [Range(1, 100, ErrorMessage = "FieldRange")] int PageSize = 50);
