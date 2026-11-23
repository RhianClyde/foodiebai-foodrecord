using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private static FlowLayoutPanel CreateButtonPanel(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Padding(0),
            Padding = new Padding(0),
            WrapContents = false
        };

        foreach (var button in buttons)
        {
            button.AutoSize = false;
            button.Height = 30;
            button.Margin = new Padding(0, 5, 5, 5);
            button.Width = button == buttons[^1] ? 95 : 100;
            panel.Controls.Add(button);
        }

        return panel;
    }

    private void ConfigureFoodsGrid()
    {
        _foodsGrid.AutoGenerateColumns = false;
        _foodsGrid.Columns.Clear();
        _foodsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(FoodRow.Id),
                HeaderText = "Document ID",
                Name = "DocumentId",
                Visible = false
            });
        _foodsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DataPropertyName = nameof(FoodRow.Name),
                FillWeight = 70,
                HeaderText = "Food name",
                MinimumWidth = 150,
                Name = "FoodName"
            });
        _foodsGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DataPropertyName = nameof(FoodRow.Price),
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" },
                FillWeight = 30,
                HeaderText = "Price",
                MinimumWidth = 100,
                Name = "Price"
            });
    }

    private void ConfigureUsersGrid()
    {
        _usersGrid.AutoGenerateColumns = false;
        _usersGrid.Columns.Clear();
        _usersGrid.AllowUserToAddRows = false;
        _usersGrid.AllowUserToDeleteRows = false;
        _usersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _usersGrid.MultiSelect = false;
        _usersGrid.ReadOnly = true;
        _usersGrid.RowHeadersVisible = false;
        _usersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _usersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserRow.Uid),
                FillWeight = 25,
                HeaderText = "UID",
                MinimumWidth = 150,
                Name = "Uid"
            });
        _usersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserRow.Username),
                FillWeight = 25,
                HeaderText = "Username",
                MinimumWidth = 120,
                Name = "Username"
            });
        _usersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserRow.Email),
                FillWeight = 30,
                HeaderText = "Email",
                MinimumWidth = 180,
                Name = "Email"
            });
        _usersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(UserRow.CreatedAt),
                FillWeight = 20,
                HeaderText = "Created",
                MinimumWidth = 150,
                Name = "CreatedAt"
            });
    }

    private void ConfigureOrdersGrid()
    {
        _ordersGrid.AutoGenerateColumns = false;
        _ordersGrid.Columns.Clear();
        _ordersGrid.AllowUserToAddRows = false;
        _ordersGrid.AllowUserToDeleteRows = false;
        _ordersGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _ordersGrid.MultiSelect = false;
        _ordersGrid.ReadOnly = true;
        _ordersGrid.RowHeadersVisible = false;
        _ordersGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _ordersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.OrderedAt),
                FillWeight = 18,
                HeaderText = "Ordered",
                MinimumWidth = 130,
                Name = "OrderedAt"
            });
        _ordersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.CustomerDisplay),
                FillWeight = 28,
                HeaderText = "Customer",
                MinimumWidth = 150,
                Name = "Customer"
            });
        _ordersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.FoodName),
                FillWeight = 24,
                HeaderText = "Food",
                MinimumWidth = 130,
                Name = "Food"
            });
        _ordersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.Quantity),
                FillWeight = 10,
                HeaderText = "Qty",
                MinimumWidth = 55,
                Name = "Quantity"
            });
        _ordersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.UnitPrice),
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" },
                FillWeight = 10,
                HeaderText = "Unit price",
                MinimumWidth = 85,
                Name = "UnitPrice"
            });
        _ordersGrid.Columns.Add(
            new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.TotalAmount),
                DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" },
                FillWeight = 10,
                HeaderText = "Total",
                MinimumWidth = 85,
                Name = "TotalAmount"
            });
    }

}
