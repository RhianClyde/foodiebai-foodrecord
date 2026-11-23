using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private void SetFoodActionsEnabled(bool enabled)
    {
        _isBusy = !enabled;
        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        var signedIn = !string.IsNullOrWhiteSpace(_firebaseIdToken);
        var canUseActions = signedIn && !_isBusy;

        _saveButton.Enabled = canUseActions;
        _updateButton.Enabled = canUseActions && !string.IsNullOrWhiteSpace(_selectedDocumentId);
        _deleteButton.Enabled = canUseActions && !string.IsNullOrWhiteSpace(_selectedDocumentId);
        _refreshButton.Enabled = canUseActions;
        _usersRefreshButton.Enabled = canUseActions;
        _userAddButton.Enabled = canUseActions;
        _userUpdateButton.Enabled =
            canUseActions && !string.IsNullOrWhiteSpace(_selectedUserDocumentId);
        _userDeleteButton.Enabled =
            canUseActions && !string.IsNullOrWhiteSpace(_selectedUserDocumentId);
        _userClearButton.Enabled = !_isBusy;
        _ordersRefreshButton.Enabled = canUseActions;
        _orderAddButton.Enabled = canUseActions;
        _orderUpdateButton.Enabled =
            canUseActions && !string.IsNullOrWhiteSpace(_selectedOrderDocumentId);
        _orderDeleteButton.Enabled =
            canUseActions && !string.IsNullOrWhiteSpace(_selectedOrderDocumentId);
        _orderClearButton.Enabled = !_isBusy;
        _clearButton.Enabled = !_isBusy;
    }

    private HttpRequestMessage CreateFirestoreRequest(
        HttpMethod method,
        string endpoint,
        object? body = null)
    {
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _firebaseIdToken);

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return request;
    }

    private static string GetFoodsEndpoint()
    {
        return
            $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(FirebaseProjectId)}/databases/(default)/documents/foods";
    }

    private static string GetUsersEndpoint()
    {
        return
            $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(FirebaseProjectId)}/databases/(default)/documents/users";
    }

    private static string GetOrdersEndpoint()
    {
        return
            $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(FirebaseProjectId)}/databases/(default)/documents/orders";
    }

    private static string GetFoodDocumentEndpoint(string documentId)
    {
        return $"{GetFoodsEndpoint()}/{Uri.EscapeDataString(documentId)}";
    }

    private static string GetUserDocumentEndpoint(string userId)
    {
        return
            $"https://firestore.googleapis.com/v1/projects/{Uri.EscapeDataString(FirebaseProjectId)}/databases/(default)/documents/users/{Uri.EscapeDataString(userId)}";
    }

    private static string GetOrderDocumentEndpoint(string documentId)
    {
        return $"{GetOrdersEndpoint()}/{Uri.EscapeDataString(documentId)}";
    }

    private static Dictionary<string, object> BuildFoodFields(
        string foodName,
        decimal price,
        bool includeCreatedAt)
    {
        var fields = new Dictionary<string, object>
        {
            ["name"] = new { stringValue = foodName },
            ["price"] = new { doubleValue = (double)price }
        };

        var timestamp = new { timestampValue = DateTime.UtcNow.ToString("O") };
        fields[includeCreatedAt ? "createdAt" : "updatedAt"] = timestamp;
        return fields;
    }

    private static Dictionary<string, object> BuildOrderFields(
        UserRow user,
        FoodRow food,
        int quantity,
        bool includeOrderedAt)
    {
        var customerName = string.IsNullOrWhiteSpace(user.Username)
            ? user.Email
            : user.Username;
        var totalAmount = food.Price * quantity;
        var fields = new Dictionary<string, object>
        {
            ["userId"] = new { stringValue = user.Uid },
            ["customerName"] = new { stringValue = customerName },
            ["customerEmail"] = new { stringValue = user.Email },
            ["foodId"] = new { stringValue = food.Id },
            ["foodName"] = new { stringValue = food.Name },
            ["quantity"] = new
            {
                integerValue = quantity.ToString(CultureInfo.InvariantCulture)
            },
            ["unitPrice"] = new { doubleValue = (double)food.Price },
            ["totalAmount"] = new { doubleValue = (double)totalAmount }
        };

        var timestamp = new { timestampValue = DateTime.UtcNow.ToString("O") };
        fields[includeOrderedAt ? "orderedAt" : "updatedAt"] = timestamp;
        return fields;
    }

    private static string? GetDocumentId(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            if (!document.RootElement.TryGetProperty("name", out var nameProperty))
            {
                return null;
            }

            var documentName = nameProperty.GetString();
            if (string.IsNullOrWhiteSpace(documentName))
            {
                return null;
            }

            var lastSlash = documentName.LastIndexOf('/');
            return lastSlash >= 0 && lastSlash < documentName.Length - 1
                ? documentName[(lastSlash + 1)..]
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ReadStringField(JsonElement fields, string fieldName)
    {
        return fields.TryGetProperty(fieldName, out var field) &&
               field.TryGetProperty("stringValue", out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static string ReadFirstStringField(
        JsonElement fields,
        params string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var value = ReadStringField(fields, fieldName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string ReadOrderFoodName(JsonElement fields)
    {
        var foodName = ReadFirstStringField(fields, "foodName", "productName");
        if (!string.IsNullOrWhiteSpace(foodName))
        {
            return foodName;
        }

        var itemsSummary = ReadStringField(fields, "itemsSummary").Trim();
        var separatorIndex = itemsSummary.IndexOf("x", StringComparison.OrdinalIgnoreCase);
        return separatorIndex >= 0 && separatorIndex < itemsSummary.Length - 1
            ? itemsSummary[(separatorIndex + 1)..].Trim()
            : itemsSummary;
    }

    private static int ReadOrderQuantity(JsonElement fields)
    {
        var quantity = ReadIntegerField(fields, "quantity");
        if (quantity > 0)
        {
            return quantity;
        }

        var itemsSummary = ReadStringField(fields, "itemsSummary").Trim();
        var separatorIndex = itemsSummary.IndexOf("x", StringComparison.OrdinalIgnoreCase);
        if (separatorIndex <= 0 ||
            !int.TryParse(
                itemsSummary[..separatorIndex].Trim(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out quantity))
        {
            return 0;
        }

        return quantity;
    }

    private static DateTimeOffset? ReadOrderTimestampValue(JsonElement fields)
    {
        return ReadTimestampValue(fields, "orderedAt") ??
               ReadTimestampValue(fields, "createdAt") ??
               ReadTextTimestampValue(fields, "orderTime");
    }

    private static DateTimeOffset? ReadTextTimestampValue(
        JsonElement fields,
        string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var field) ||
            !field.TryGetProperty("stringValue", out var value) ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            return null;
        }

        var timestampText = value.GetString();
        if (DateTimeOffset.TryParse(
                timestampText,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var timestamp) ||
            DateTimeOffset.TryParse(
                timestampText,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out timestamp))
        {
            return timestamp;
        }

        return null;
    }

    private static string ReadTimestampField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var field) ||
            !field.TryGetProperty("timestampValue", out var value) ||
            !DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return string.Empty;
        }

        return timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    private static decimal ReadNumberField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var field))
        {
            return 0M;
        }

        if (field.TryGetProperty("doubleValue", out var doubleValue))
        {
            return (decimal)doubleValue.GetDouble();
        }

        if (field.TryGetProperty("integerValue", out var integerValue) &&
            decimal.TryParse(
                integerValue.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var integer))
        {
            return integer;
        }

        return 0M;
    }

    private static int ReadIntegerField(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var field) ||
            !field.TryGetProperty("integerValue", out var integerValue) ||
            !int.TryParse(
                integerValue.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value))
        {
            return 0;
        }

        return value;
    }

    private static DateTimeOffset? ReadTimestampValue(JsonElement fields, string fieldName)
    {
        if (!fields.TryGetProperty(fieldName, out var field) ||
            !field.TryGetProperty("timestampValue", out var value) ||
            !DateTimeOffset.TryParse(
                value.GetString(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return null;
        }

        return timestamp;
    }

    private static bool IsFirebaseConfigured()
    {
        return !FirebaseProjectId.StartsWith("PASTE_", StringComparison.OrdinalIgnoreCase) &&
               !FirebaseWebApiKey.StartsWith("PASTE_", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFirebaseError(string responseBody)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBody);
            return document.RootElement
                .GetProperty("error")
                .GetProperty("message")
                .GetString() ?? "Unknown Firebase error";
        }
        catch (JsonException)
        {
            return responseBody.Length > 200 ? responseBody[..200] : responseBody;
        }
    }

    private void SetStatus(string message, Color color)
    {
        _statusLabel.Text = message;
        _statusLabel.ForeColor = color;
    }

}
