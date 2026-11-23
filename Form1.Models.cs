using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private sealed class FirebaseAuthResponse
    {
        [JsonPropertyName("idToken")]
        public string? IdToken { get; set; }

        [JsonPropertyName("localId")]
        public string? LocalId { get; set; }
    }

    private sealed class FoodRow
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public decimal Price { get; init; }
        public string DisplayName => $"{Name} ({Price:C2})";
    }

    private sealed class UserRow
    {
        public string DocumentId { get; init; } = string.Empty;
        public string Uid { get; init; } = string.Empty;
        public string Username { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string DisplayName => string.IsNullOrWhiteSpace(Username)
            ? Email
            : string.IsNullOrWhiteSpace(Email)
                ? Username
                : $"{Username} ({Email})";
    }

    private sealed class OrderRow
    {
        public string DocumentId { get; init; } = string.Empty;
        public string UserId { get; init; } = string.Empty;
        public string CustomerName { get; init; } = string.Empty;
        public string CustomerEmail { get; init; } = string.Empty;
        public string FoodId { get; init; } = string.Empty;
        public string FoodName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal UnitPrice { get; init; }
        public decimal TotalAmount { get; init; }
        public DateTimeOffset OrderedAtValue { get; init; }
        public string OrderedAt { get; init; } = string.Empty;
        public string CustomerDisplay => string.IsNullOrWhiteSpace(CustomerEmail)
            ? CustomerName
            : $"{CustomerName} ({CustomerEmail})";
    }
}
