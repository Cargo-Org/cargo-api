using Cargo.BuildingBlocks.Extensions;
using Cargo.CustomerService.Domain.Enums;
using Cargo.CustomerService.Features.Documents.GetMyDocuments;
using Cargo.CustomerService.Features.Documents.GetUploadUrl;
using Cargo.CustomerService.Features.Documents.RegisterDocument;
using Cargo.CustomerService.Features.Documents.ReviewDocument;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Cargo.CustomerService.Features.Documents;

// Request body record — defined here since it is only used by the endpoint.
// KeycloakUserId is never in the request body — always extracted from JWT.
public sealed record RegisterDocumentRequest(
    DocumentType DocumentType,
    string ObjectKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes);

public sealed record ReviewDocumentRequest(
    DocumentReviewStatus Status,
    string? ReviewNote);

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(
        this IEndpointRouteBuilder app)
    {
        // ── GET /me/documents/upload-url ───────────────────────────────────
        app.MapGet("/me/documents/upload-url", async (
            [FromQuery] DocumentType documentType,
            [FromQuery] string contentType,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var query = new GetUploadUrlQuery(
                documentType,
                contentType,
                keycloakUserId);

            var result = await sender.Send(query, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("GetUploadUrl")
        .WithSummary("Get a pre-signed S3 URL for uploading a document")
        .WithDescription(
            "Generates a secure, temporary URL for direct-to-S3 uploads. " +
            "The client must upload using the provided HTTP verb and Content-Type.");

        // ── POST /me/documents ─────────────────────────────────────────────
        app.MapPost("/me/documents", async (
            RegisterDocumentRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new RegisterDocumentCommand(
                keycloakUserId,
                request.DocumentType,
                request.ObjectKey,
                request.OriginalFileName,
                request.ContentType,
                request.FileSizeBytes);

            var result = await sender.Send(command, ct);

            return result.Match(
                success => Results.Created($"/me/documents/{success}", new { Id = success }),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("RegisterDocument")
        .WithSummary("Register a newly uploaded document")
        .WithDescription(
            "Registers a document in the system after it has been successfully uploaded to S3. " +
            "The file size constraint (15MB) is enforced here. Automatically recalculates OnboardingStatus.");

        // ── GET /me/documents ──────────────────────────────────────────────
        app.MapGet("/me/documents", async (
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var keycloakUserId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var query = new GetMyDocumentsQuery(keycloakUserId);

            var result = await sender.Send(query, ct);

            return result.Match(
                success => Results.Ok(success),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization()
        .WithName("GetMyDocuments")
        .WithSummary("List all documents for the authenticated customer")
        .WithDescription(
            "Returns a list of documents. Approved documents will include a secure, " +
            "temporary download URL valid for 60 minutes.");

        // ── PATCH /admin/documents/{id}/review ─────────────────────────────
        app.MapPatch("/admin/documents/{id:guid}/review", async (
            Guid id,
            ReviewDocumentRequest request,
            ClaimsPrincipal user,
            ISender sender,
            CancellationToken ct) =>
        {
            var reviewerId = user.FindFirstValue("sub")
                ?? throw new InvalidOperationException("sub claim missing.");

            var command = new ReviewDocumentCommand(
                id,
                reviewerId,
                request.Status,
                request.ReviewNote);

            var result = await sender.Send(command, ct);

            return result.Match(
                _ => Results.Ok(),
                errors => errors.ToProblemResult());
        })
        .RequireAuthorization("AdminPolicy") // CRITICAL: Only admins can hit this
        .WithName("ReviewDocument")
        .WithSummary("Approve or reject a document (Admin Only)")
        .WithDescription(
            "Updates document status, sets the reviewer ID, and recalculates " +
            "the customer's onboarding status atomically.");

        return app;
    }


}