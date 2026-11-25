using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private async void SaveButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() || !TryReadFoodInput(out var foodName, out var price))
        {
            return;
        }

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Adding food...", Color.DarkOrange);

            using var request = CreateFirestoreRequest(
                HttpMethod.Post,
                GetFoodsEndpoint(),
                new
                {
                    fields = BuildFoodFields(foodName, price, includeCreatedAt: true)
                });

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Add failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            var documentId = GetDocumentId(responseBody);
            if (string.IsNullOrWhiteSpace(documentId))
            {
                SetStatus("Food was added, but Firebase did not return its document ID.", Color.Firebrick);
                return;
            }

            ClearFoodEditor();
            SetStatus($"Food added successfully. Document ID: {documentId}", Color.DarkGreen);

            await LoadFoodsAsync();
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Network error: {ex.Message}", Color.Firebrick);
        }
        catch (JsonException ex)
        {
            SetStatus($"Invalid Firebase response: {ex.Message}", Color.Firebrick);
        }
        finally
        {
            SetFoodActionsEnabled(true);
        }
    }

    private async void UpdateButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() || string.IsNullOrWhiteSpace(_selectedDocumentId) ||
            !TryReadFoodInput(out var foodName, out var price))
        {
            return;
        }

        var documentId = _selectedDocumentId;

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Updating food...", Color.DarkOrange);

            var endpoint =
                $"{GetFoodDocumentEndpoint(documentId)}?updateMask.fieldPaths=name&updateMask.fieldPaths=price&updateMask.fieldPaths=updatedAt";

            using var request = CreateFirestoreRequest(
                HttpMethod.Patch,
                endpoint,
                new
                {
                    fields = BuildFoodFields(foodName, price, includeCreatedAt: false)
                });

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Update failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            ClearFoodEditor();
            SetStatus("Food updated successfully.", Color.DarkGreen);
            await LoadFoodsAsync();
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Network error: {ex.Message}", Color.Firebrick);
        }
        catch (JsonException ex)
        {
            SetStatus($"Invalid Firebase response: {ex.Message}", Color.Firebrick);
        }
        finally
        {
            SetFoodActionsEnabled(true);
        }
    }

    private async void DeleteButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() || string.IsNullOrWhiteSpace(_selectedDocumentId))
        {
            SetStatus("Select a food record before deleting.", Color.Firebrick);
            return;
        }

        var documentId = _selectedDocumentId;
        var answer = MessageBox.Show(
            this,
            "Delete the selected food record? This cannot be undone.",
            "Confirm delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Deleting food...", Color.DarkOrange);

            using var request = CreateFirestoreRequest(
                HttpMethod.Delete,
                GetFoodDocumentEndpoint(documentId));
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Delete failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            ClearFoodEditor();
            SetStatus("Food deleted successfully.", Color.DarkGreen);
            await LoadFoodsAsync();
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Network error: {ex.Message}", Color.Firebrick);
        }
        finally
        {
            SetFoodActionsEnabled(true);
        }
    }

    private void ClearButton_Click(object? sender, EventArgs e)
    {
        ClearFoodEditor();
        SetStatus("Ready to add a new food.", Color.DarkOrange);
    }

    private async void RefreshButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn())
        {
            return;
        }

        SetStatus("Refreshing foods...", Color.DarkOrange);
        if (await LoadFoodsAsync())
        {
            SetStatus("Food records refreshed.", Color.DarkGreen);
        }
    }


    private async Task<bool> LoadFoodsAsync()
    {
        if (string.IsNullOrWhiteSpace(_firebaseIdToken))
        {
            return false;
        }

        try
        {
            var endpoint = $"{GetFoodsEndpoint()}?pageSize=100";
            using var request = CreateFirestoreRequest(HttpMethod.Get, endpoint);

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Could not load foods: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return false;
            }

            using var document = JsonDocument.Parse(responseBody);
            var foods = new List<FoodRow>();

            if (document.RootElement.TryGetProperty("documents", out var documents))
            {
                foreach (var item in documents.EnumerateArray())
                {
                    var documentName = item.GetProperty("name").GetString() ?? string.Empty;
                    var fields = item.GetProperty("fields");

                    foods.Add(
                        new FoodRow
                        {
                            Id = documentName[(documentName.LastIndexOf('/') + 1)..],
                            Name = ReadStringField(fields, "name"),
                            Price = ReadNumberField(fields, "price")
                        });
                }
            }

            _foodRows = foods
                .OrderBy(food => food.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(food => food.Price)
                .ToList();
            _foodsGrid.DataSource = _foodRows;
            BindOrderFoodOptions(_foodRows);
            ClearFoodEditor();
            return true;
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Network error: {ex.Message}", Color.Firebrick);
            return false;
        }
        catch (JsonException ex)
        {
            SetStatus($"Invalid Firebase response: {ex.Message}", Color.Firebrick);
            return false;
        }
    }


    private bool TryReadFoodInput(out string foodName, out decimal price)
    {
        foodName = string.Join(
            ' ',
            _foodNameTextBox.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        price = decimal.Round(_priceInput.Value, 2, MidpointRounding.AwayFromZero);

        if (string.IsNullOrWhiteSpace(foodName))
        {
            SetStatus("Enter a food name.", Color.Firebrick);
            _foodNameTextBox.Focus();
            return false;
        }

        if (foodName.Length > 100)
        {
            SetStatus("Food name must be 100 characters or fewer.", Color.Firebrick);
            _foodNameTextBox.Focus();
            return false;
        }

        if (price < _priceInput.Minimum || price > _priceInput.Maximum)
        {
            SetStatus("Enter a valid price.", Color.Firebrick);
            _priceInput.Focus();
            return false;
        }

        _foodNameTextBox.Text = foodName;
        _priceInput.Value = price;
        return true;
    }

    private void FoodsGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_foodsGrid.SelectedRows.Count == 0 ||
            _foodsGrid.SelectedRows[0].DataBoundItem is not FoodRow food)
        {
            _selectedDocumentId = null;
            UpdateActionButtons();
            return;
        }

        _selectedDocumentId = food.Id;
        _foodNameTextBox.Text = food.Name;
        _priceInput.Value = Math.Min(
            Math.Max(food.Price, _priceInput.Minimum),
            _priceInput.Maximum);
        _documentIdLabel.Text = $"Document ID: {food.Id}";
        UpdateActionButtons();
    }

    private void ClearFoodEditor()
    {
        _selectedDocumentId = null;
        _foodNameTextBox.Clear();
        _priceInput.Value = _priceInput.Minimum;
        _documentIdLabel.Text = "Document ID: -";

        if (_foodsGrid.Rows.Count > 0)
        {
            _foodsGrid.CurrentCell = null;
            _foodsGrid.ClearSelection();
        }

        UpdateActionButtons();
    }

}
