using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace SanitizeService.Api.OpenApi;

/// <summary>
/// Removes <c>components/schemas</c> entries that are not reachable from <c>paths</c> (drops framework-only noise).
/// </summary>
internal static class OpenApiReferencedSchemaPruner
{
    public static void Register(OpenApiOptions options)
    {
        options.AddDocumentTransformer(PruneUnreferencedSchemas);
    }

    private static Task PruneUnreferencedSchemas(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext _,
        CancellationToken __)
    {
        if (document.Components?.Schemas is null || document.Components.Schemas.Count == 0)
        {
            return Task.CompletedTask;
        }

        var referenced = new HashSet<string>(StringComparer.Ordinal);
        if (document.Paths is not null)
        {
            foreach (var pathItem in document.Paths.Values)
            {
                if (pathItem?.Operations is null)
                {
                    continue;
                }

                foreach (var operation in pathItem.Operations.Values)
                {
                    VisitOperation(operation, referenced);
                }
            }
        }

        var schemas = document.Components.Schemas;
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var id in referenced.ToArray())
            {
                if (!schemas.TryGetValue(id, out var schema) || schema is null)
                {
                    continue;
                }

                var before = referenced.Count;
                VisitSchema(schema, referenced);
                if (referenced.Count > before)
                {
                    changed = true;
                }
            }
        }

        foreach (var key in schemas.Keys.ToArray())
        {
            if (!referenced.Contains(key))
            {
                schemas.Remove(key);
            }
        }

        return Task.CompletedTask;
    }

    private static void VisitOperation(OpenApiOperation? operation, HashSet<string> referenced)
    {
        if (operation is null)
        {
            return;
        }

        if (operation.RequestBody?.Content is not null)
        {
            foreach (var media in operation.RequestBody.Content.Values)
            {
                VisitSchema(media.Schema, referenced);
            }
        }

        if (operation.Responses is not null)
        {
            foreach (var response in operation.Responses.Values)
            {
                if (response?.Content is null)
                {
                    continue;
                }

                foreach (var media in response.Content.Values)
                {
                    VisitSchema(media.Schema, referenced);
                }
            }
        }

        if (operation.Parameters is not null)
        {
            foreach (var parameter in operation.Parameters)
            {
                if (parameter is OpenApiParameter { Schema: { } schema })
                {
                    VisitSchema(schema, referenced);
                }
            }
        }
    }

    private static void VisitSchema(IOpenApiSchema? schema, HashSet<string> referenced)
    {
        if (schema is null)
        {
            return;
        }

        if (schema is OpenApiSchemaReference schemaRef)
        {
            if (!string.IsNullOrEmpty(schemaRef.Id))
            {
                referenced.Add(schemaRef.Id);
            }

            return;
        }

        if (schema is not OpenApiSchema s)
        {
            return;
        }

        if (s.Reference?.Id is { } refId && s.Reference.Type == ReferenceType.Schema)
        {
            referenced.Add(refId);
        }

        VisitSchemaCollection(s.AllOf, referenced);
        VisitSchemaCollection(s.AnyOf, referenced);
        VisitSchemaCollection(s.OneOf, referenced);
        if (s.Not is not null)
        {
            VisitSchema(s.Not, referenced);
        }

        if (s.Items is not null)
        {
            VisitSchema(s.Items, referenced);
        }

        if (s.Properties is not null)
        {
            foreach (var child in s.Properties.Values)
            {
                VisitSchema(child, referenced);
            }
        }

        if (s.AdditionalProperties is not null)
        {
            VisitSchema(s.AdditionalProperties, referenced);
        }
    }

    private static void VisitSchemaCollection(IList<IOpenApiSchema>? list, HashSet<string> referenced)
    {
        if (list is null)
        {
            return;
        }

        foreach (var item in list)
        {
            VisitSchema(item, referenced);
        }
    }
}
