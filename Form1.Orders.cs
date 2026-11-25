using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private async void AddOrderButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() ||
            !TryReadOrderInput(
                out var selectedUser,
                out var selectedFood,
                out var quantity,
                out var orderTotal))
        {
            return;
        }

        if (selectedUser is null || selectedFood is null)
        {
            return;
        }

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Recording order...", Color.DarkOrange);

            using var request = CreateFirestoreRequest(
                HttpMethod.Post,
                GetOrdersEndpoint(),
                new
                {
                    fields = BuildOrderFields(
                        selectedUser,
                        selectedFood,
                        quantity,
                        includeOrderedAt: true)
                });
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Order add failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            ClearOrderEditor();
            SetStatus($"Order recorded. Sale total: {orderTotal:C2}", Color.DarkGreen);
            await LoadOrdersAsync();
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

    private async void UpdateOrderButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() ||
            string.IsNullOrWhiteSpace(_selectedOrderDocumentId) ||
            !TryReadOrderInput(
                out var selectedUser,
                out var selectedFood,
                out var quantity,
                out var orderTotal))
        {
            return;
        }

        if (selectedUser is null || selectedFood is null)
        {
            return;
        }

        var documentId = _selectedOrderDocumentId;

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Updating order...", Color.DarkOrange);

            var endpoint =
                $"{GetOrderDocumentEndpoint(documentId)}?updateMask.fieldPaths=userId" +
                "&updateMask.fieldPaths=customerName" +
                "&updateMask.fieldPaths=customerEmail" +
                "&updateMask.fieldPaths=foodId" +
                "&updateMask.fieldPaths=foodName" +
                "&updateMask.fieldPaths=quantity" +
                "&updateMask.fieldPaths=unitPrice" +
                "&updateMask.fieldPaths=totalAmount" +
                "&updateMask.fieldPaths=updatedAt";
            using var request = CreateFirestoreRequest(
                HttpMethod.Patch,
                endpoint,
                new
                {
                    fields = BuildOrderFields(
                        selectedUser,
                        selectedFood,
                        quantity,
                        includeOrderedAt: false)
                });
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Order update failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            ClearOrderEditor();
            SetStatus($"Order updated. Sale total: {orderTotal:C2}", Color.DarkGreen);
            await LoadOrdersAsync();
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

    private async void DeleteOrderButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() || string.IsNullOrWhiteSpace(_selectedOrderDocumentId))
        {
            SetStatus("Select an order record before deleting.", Color.Firebrick);
            return;
        }

        var documentId = _selectedOrderDocumentId;
        var answer = MessageBox.Show(
            this,
            "Delete the selected order record? This will remove it from the sales total.",
            "Confirm order delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Deleting order...", Color.DarkOrange);

            using var request = CreateFirestoreRequest(
                HttpMethod.Delete,
                GetOrderDocumentEndpoint(documentId));
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Order delete failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            ClearOrderEditor();
            SetStatus("Order deleted and sales total recalculated.", Color.DarkGreen);
            await LoadOrdersAsync();
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

    private void ClearOrderButton_Click(object? sender, EventArgs e)
    {
        ClearOrderEditor();
        SetStatus("Ready to record a new order.", Color.DarkOrange);
    }

    private async void OrdersRefreshButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn())
        {
            return;
        }

        SetStatus("Refreshing orders...", Color.DarkOrange);
        if (await LoadOrdersAsync())
        {
            SetStatus("Orders and sales total refreshed.", Color.DarkGreen);
        }
    }

    private async void OrdersPollingTimer_Tick(object? sender, EventArgs e)
    {
        if (_ordersPollingInProgress ||
            _isBusy ||
            !_ordersTabIsSelected ||
            string.IsNullOrWhiteSpace(_firebaseIdToken))
        {
            return;
        }

        _ordersPollingInProgress = true;
        try
        {
            await LoadOrdersAsync();
        }
        finally
        {
            _ordersPollingInProgress = false;
        }
    }

    private void StartOrdersPolling()
    {
        _ordersPollingTimer.Start();
    }

    private void OrderFoodComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        UpdateOrderSummary();
    }

    private void OrderQuantityInput_ValueChanged(object? sender, EventArgs e)
    {
        UpdateOrderSummary();
    }

    private void OrdersGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_isBindingOrdersGrid)
        {
            return;
        }

        if (_ordersGrid.SelectedRows.Count == 0 ||
            _ordersGrid.SelectedRows[0].DataBoundItem is not OrderRow order)
        {
            _selectedOrderDocumentId = null;
            UpdateActionButtons();
            return;
        }

        _selectedOrderDocumentId = order.DocumentId;
        _orderUserComboBox.SelectedItem = _userRows.FirstOrDefault(
            user => string.Equals(user.Uid, order.UserId, StringComparison.Ordinal)) ??
            _userRows.FirstOrDefault(
                user => string.Equals(user.DocumentId, order.UserId, StringComparison.Ordinal));
        _orderFoodComboBox.SelectedItem = _foodRows.FirstOrDefault(
            food => string.Equals(food.Id, order.FoodId, StringComparison.Ordinal));
        _orderQuantityInput.Value = Math.Min(
            Math.Max(order.Quantity, (int)_orderQuantityInput.Minimum),
            (int)_orderQuantityInput.Maximum);
        _orderDocumentIdLabel.Text = $"Order document: {order.DocumentId}";
        UpdateOrderSummary();
        UpdateActionButtons();
    }

    private void ClearOrderEditor()
    {
        _selectedOrderDocumentId = null;
        _orderUserComboBox.SelectedIndex = -1;
        _orderFoodComboBox.SelectedIndex = -1;
        _orderQuantityInput.Value = _orderQuantityInput.Minimum;
        _orderDocumentIdLabel.Text = "Order document: -";
        UpdateOrderSummary();

        if (_ordersGrid.Rows.Count > 0)
        {
            _ordersGrid.CurrentCell = null;
            _ordersGrid.ClearSelection();
        }

        UpdateActionButtons();
    }

    private bool TryReadOrderInput(
        out UserRow? selectedUser,
        out FoodRow? selectedFood,
        out int quantity,
        out decimal orderTotal)
    {
        selectedUser = _orderUserComboBox.SelectedItem as UserRow;
        selectedFood = _orderFoodComboBox.SelectedItem as FoodRow;
        quantity = (int)_orderQuantityInput.Value;
        orderTotal = 0M;

        if (selectedUser is null || string.IsNullOrWhiteSpace(selectedUser.Uid))
        {
            SetStatus("Select the user who placed the order.", Color.Firebrick);
            _orderUserComboBox.Focus();
            return false;
        }

        if (selectedFood is null || string.IsNullOrWhiteSpace(selectedFood.Id))
        {
            SetStatus("Select the food that was ordered.", Color.Firebrick);
            _orderFoodComboBox.Focus();
            return false;
        }

        if (quantity < 1)
        {
            SetStatus("Order quantity must be at least 1.", Color.Firebrick);
            _orderQuantityInput.Focus();
            return false;
        }

        orderTotal = selectedFood.Price * quantity;
        if (orderTotal <= 0M)
        {
            SetStatus("The selected food has an invalid price.", Color.Firebrick);
            return false;
        }

        return true;
    }

    private void UpdateOrderSummary()
    {
        var unitPrice = (_orderFoodComboBox.SelectedItem as FoodRow)?.Price ?? 0M;
        var orderTotal = unitPrice * _orderQuantityInput.Value;
        _orderSummaryLabel.Text =
            $"Unit price: {unitPrice:C2} | Order total: {orderTotal:C2}";
    }


    private async Task<bool> LoadOrdersAsync()
    {
        if (string.IsNullOrWhiteSpace(_firebaseIdToken))
        {
            return false;
        }

        var loadVersion = ++_ordersLoadVersion;

        try
        {
            var endpoint = $"{GetOrdersEndpoint()}?pageSize=100";
            using var request = CreateFirestoreRequest(HttpMethod.Get, endpoint);
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (loadVersion != _ordersLoadVersion)
            {
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Could not load orders: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return false;
            }

            using var document = JsonDocument.Parse(responseBody);
            var orders = new List<OrderRow>();

            if (document.RootElement.TryGetProperty("documents", out var documents))
            {
                foreach (var item in documents.EnumerateArray())
                {
                    var documentName = item.GetProperty("name").GetString() ?? string.Empty;
                    var lastSlash = documentName.LastIndexOf('/');
                    var documentId = lastSlash >= 0
                        ? documentName[(lastSlash + 1)..]
                        : documentName;
                    var fields = item.GetProperty("fields");
                    var userId = ReadFirstStringField(fields, "userId", "uid");
                    var customerName = ReadFirstStringField(
                        fields,
                        "customerName",
                        "username");
                    var customerEmail = ReadFirstStringField(
                        fields,
                        "customerEmail",
                        "email");
                    var foodId = ReadFirstStringField(fields, "foodId", "productId");
                    var foodName = ReadOrderFoodName(fields);
                    var quantity = ReadOrderQuantity(fields);
                    var unitPrice = ReadNumberField(fields, "unitPrice");
                    var totalAmount = ReadNumberField(fields, "totalAmount");
                    if (totalAmount <= 0M)
                    {
                        totalAmount = ReadNumberField(fields, "totalPrice");
                    }
                    if (totalAmount <= 0M)
                    {
                        totalAmount = unitPrice * quantity;
                    }
                    if (unitPrice <= 0M && quantity > 0 && totalAmount > 0M)
                    {
                        unitPrice = decimal.Round(
                            totalAmount / quantity,
                            2,
                            MidpointRounding.AwayFromZero);
                    }

                    if (string.IsNullOrWhiteSpace(userId) &&
                        string.IsNullOrWhiteSpace(customerName) &&
                        string.IsNullOrWhiteSpace(customerEmail) &&
                        string.IsNullOrWhiteSpace(foodId) &&
                        string.IsNullOrWhiteSpace(foodName) &&
                        quantity <= 0 &&
                        unitPrice <= 0M &&
                        totalAmount <= 0M)
                    {
                        continue;
                    }

                    var orderedAtValue =
                        ReadOrderTimestampValue(fields) ?? DateTimeOffset.MinValue;

                    orders.Add(
                        new OrderRow
                        {
                            DocumentId = documentId,
                            UserId = userId,
                            CustomerName = customerName,
                            CustomerEmail = customerEmail,
                            FoodId = foodId,
                            FoodName = foodName,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            TotalAmount = totalAmount,
                            OrderedAtValue = orderedAtValue,
                            OrderedAt = orderedAtValue == DateTimeOffset.MinValue
                                ? string.Empty
                                : orderedAtValue.ToLocalTime().ToString(
                                    "g",
                                    CultureInfo.CurrentCulture)
                        });
                }
            }

            var selectedOrderId = _selectedOrderDocumentId;
            var sortedOrders = orders
                .OrderByDescending(order => order.OrderedAtValue)
                .ThenBy(order => order.CustomerName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();

            _isBindingOrdersGrid = true;
            try
            {
                _ordersGrid.DataSource = null;
                _ordersGrid.DataSource = sortedOrders;
                _ordersGrid.CurrentCell = null;
                _ordersGrid.ClearSelection();
            }
            finally
            {
                _isBindingOrdersGrid = false;
            }

            var selectedOrder = string.IsNullOrWhiteSpace(selectedOrderId)
                ? null
                : sortedOrders.FirstOrDefault(
                    order => string.Equals(
                        order.DocumentId,
                        selectedOrderId,
                        StringComparison.Ordinal));

            if (selectedOrder is null)
            {
                ClearOrderEditor();
            }
            else
            {
                var selectedRow = _ordersGrid.Rows
                    .Cast<DataGridViewRow>()
                    .FirstOrDefault(row => ReferenceEquals(row.DataBoundItem, selectedOrder));
                if (selectedRow is not null)
                {
                    selectedRow.Selected = true;
                    _ordersGrid.CurrentCell = selectedRow.Cells[0];
                }
            }

            _salesSummaryLabel.Text =
                $"Total sales: {sortedOrders.Sum(order => order.TotalAmount):C2} | " +
                $"Orders: {sortedOrders.Count}";
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

    private void BindOrderUserOptions(IReadOnlyList<UserRow> users)
    {
        var selectedUserId = (_orderUserComboBox.SelectedItem as UserRow)?.Uid;
        _orderUserComboBox.DataSource = null;
        _orderUserComboBox.DisplayMember = nameof(UserRow.DisplayName);
        _orderUserComboBox.DataSource = users.ToList();
        _orderUserComboBox.SelectedIndex = -1;

        if (!string.IsNullOrWhiteSpace(selectedUserId))
        {
            _orderUserComboBox.SelectedItem = users.FirstOrDefault(
                user => string.Equals(user.Uid, selectedUserId, StringComparison.Ordinal));
        }
    }

    private void BindOrderFoodOptions(IReadOnlyList<FoodRow> foods)
    {
        var selectedFoodId = (_orderFoodComboBox.SelectedItem as FoodRow)?.Id;
        _orderFoodComboBox.DataSource = null;
        _orderFoodComboBox.DisplayMember = nameof(FoodRow.DisplayName);
        _orderFoodComboBox.DataSource = foods.ToList();
        _orderFoodComboBox.SelectedIndex = -1;

        if (!string.IsNullOrWhiteSpace(selectedFoodId))
        {
            _orderFoodComboBox.SelectedItem = foods.FirstOrDefault(
                food => string.Equals(food.Id, selectedFoodId, StringComparison.Ordinal));
        }

        UpdateOrderSummary();
    }

}
