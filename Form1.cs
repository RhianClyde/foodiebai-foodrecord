using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1 : Form
{
    private const string FirebaseProjectId = "foodiebai";
    private const string FirebaseWebApiKey = "AIzaSyBxWU7L3_IWsYbltcjaVDfhmX3wp0CMEvI";
    private const int OrdersPollingIntervalMilliseconds = 5_000;

    private readonly HttpClient _httpClient = new();
    private readonly System.Windows.Forms.Timer _ordersPollingTimer = new()
    {
        Interval = OrdersPollingIntervalMilliseconds
    };
    private readonly TextBox _emailTextBox = new();
    private readonly TextBox _passwordTextBox = new();
    private readonly TextBox _foodNameTextBox = new();
    private readonly NumericUpDown _priceInput = new();
    private readonly Button _loginButton = new();
    private readonly Button _registerButton = new();
    private readonly Button _saveButton = new();
    private readonly Button _updateButton = new();
    private readonly Button _deleteButton = new();
    private readonly Button _clearButton = new();
    private readonly Button _refreshButton = new();
    private readonly Label _documentIdLabel = new();
    private readonly Label _statusLabel = new();
    private readonly DataGridView _foodsGrid = new();
    private readonly DataGridView _usersGrid = new();
    private readonly Button _usersRefreshButton = new();
    private readonly TextBox _userUsernameTextBox = new();
    private readonly TextBox _userEmailTextBox = new();
    private readonly Label _userDocumentIdLabel = new();
    private readonly Button _userAddButton = new();
    private readonly Button _userUpdateButton = new();
    private readonly Button _userDeleteButton = new();
    private readonly Button _userClearButton = new();
    private readonly DataGridView _ordersGrid = new();
    private readonly Button _ordersRefreshButton = new();
    private readonly ComboBox _orderUserComboBox = new();
    private readonly ComboBox _orderFoodComboBox = new();
    private readonly NumericUpDown _orderQuantityInput = new();
    private readonly Label _orderSummaryLabel = new();
    private readonly Label _orderDocumentIdLabel = new();
    private readonly Label _salesSummaryLabel = new();
    private readonly Button _orderAddButton = new();
    private readonly Button _orderUpdateButton = new();
    private readonly Button _orderDeleteButton = new();
    private readonly Button _orderClearButton = new();

    private string? _firebaseIdToken;
    private string? _selectedDocumentId;
    private string? _selectedUserDocumentId;
    private string? _selectedOrderDocumentId;
    private List<UserRow> _userRows = [];
    private List<FoodRow> _foodRows = [];
    private bool _isBusy;
    private bool _ordersTabIsSelected;
    private bool _ordersPollingInProgress;
    private bool _isBindingOrdersGrid;
    private int _ordersLoadVersion;

    public Form1()
    {
        InitializeComponent();
        BuildForm();
    }

    private void BuildForm()
    {
        _ordersPollingTimer.Tick += OrdersPollingTimer_Tick;
        FormClosed += (_, _) => _ordersPollingTimer.Stop();

        Text = "Food Admin - Firebase";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(850, 580);
        MinimumSize = new Size(700, 500);

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Location = new Point(20, 15),
            Text = "Food management"
        };

        var emailLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Admin email:"
        };

        var passwordLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Password:"
        };
        _emailTextBox.MaxLength = 254;
        _passwordTextBox.UseSystemPasswordChar = true;
        _loginButton.Text = "Sign in";
        _loginButton.Click += LoginButton_Click;

        _registerButton.Text = "Register";
        _registerButton.Click += RegisterButton_Click;

        var foodNameLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Food name:"
        };

        var priceLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Price:"
        };
        _priceInput.DecimalPlaces = 2;
        _priceInput.Increment = 0.01M;
        _priceInput.Minimum = 0.01M;
        _priceInput.Maximum = 1_000_000M;
        _priceInput.ThousandsSeparator = true;
        _priceInput.Value = _priceInput.Minimum;
        _foodNameTextBox.MaxLength = 100;
        _saveButton.Text = "Add food";
        _saveButton.Enabled = false;
        _saveButton.Click += SaveButton_Click;

        _updateButton.Text = "Update";
        _updateButton.Enabled = false;
        _updateButton.Click += UpdateButton_Click;

        _deleteButton.Text = "Delete";
        _deleteButton.Enabled = false;
        _deleteButton.Click += DeleteButton_Click;

        _clearButton.Text = "Clear";
        _clearButton.Click += ClearButton_Click;

        _refreshButton.Text = "Refresh";
        _refreshButton.Enabled = false;
        _refreshButton.Click += RefreshButton_Click;

        var authLayout = new TableLayoutPanel
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            ColumnCount = 5,
            Location = new Point(20, 45),
            Margin = new Padding(0),
            RowCount = 1,
            Size = new Size(ClientSize.Width - 40, 40)
        };
        authLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        authLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        authLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        authLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        authLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 205));
        authLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        foreach (var label in new[] { emailLabel, passwordLabel, foodNameLabel, priceLabel })
        {
            label.AutoSize = false;
            label.Margin = new Padding(0);
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        foreach (var editor in new Control[] { _emailTextBox, _passwordTextBox })
        {
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(3, 5, 3, 5);
        }

        var loginPanel = CreateButtonPanel(_loginButton, _registerButton);
        authLayout.Controls.Add(emailLabel, 0, 0);
        authLayout.Controls.Add(_emailTextBox, 1, 0);
        authLayout.Controls.Add(passwordLabel, 2, 0);
        authLayout.Controls.Add(_passwordTextBox, 3, 0);
        authLayout.Controls.Add(loginPanel, 4, 0);

        _statusLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        _statusLabel.AutoSize = false;
        _statusLabel.ForeColor = Color.DarkOrange;
        _statusLabel.Location = new Point(20, 90);
        _statusLabel.Size = new Size(ClientSize.Width - 40, 25);
        _statusLabel.Text = "Enter your Firebase settings in Form1.cs, then sign in.";
        _statusLabel.TextAlign = ContentAlignment.MiddleLeft;

        var mainTabs = new TabControl
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            Location = new Point(20, 120),
            Size = new Size(ClientSize.Width - 40, ClientSize.Height - 140)
        };
        var foodsPage = new TabPage("Foods");
        var usersPage = new TabPage("Users");
        var ordersPage = new TabPage("Orders");

        foreach (var editor in new Control[] { _foodNameTextBox, _priceInput })
        {
            editor.Dock = DockStyle.Fill;
            editor.Margin = new Padding(3, 5, 3, 5);
        }

        var foodInputLayout = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Top,
            Height = 40,
            Margin = new Padding(0),
            RowCount = 1
        };
        foodInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 105));
        foodInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        foodInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 95));
        foodInputLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        foodInputLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        foodInputLayout.Controls.Add(foodNameLabel, 0, 0);
        foodInputLayout.Controls.Add(_foodNameTextBox, 1, 0);
        foodInputLayout.Controls.Add(priceLabel, 2, 0);
        foodInputLayout.Controls.Add(_priceInput, 3, 0);

        var foodActionsPanel = CreateButtonPanel(
            _saveButton,
            _updateButton,
            _deleteButton,
            _clearButton,
            _refreshButton);
        foodActionsPanel.Dock = DockStyle.Top;
        foodActionsPanel.Height = 40;

        _documentIdLabel.AutoSize = false;
        _documentIdLabel.Dock = DockStyle.Top;
        _documentIdLabel.Height = 30;
        _documentIdLabel.Margin = new Padding(0);
        _documentIdLabel.Text = "Document ID: -";
        _documentIdLabel.TextAlign = ContentAlignment.MiddleLeft;

        _foodsGrid.AllowUserToAddRows = false;
        _foodsGrid.AllowUserToDeleteRows = false;
        _foodsGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _foodsGrid.Dock = DockStyle.Fill;
        _foodsGrid.ReadOnly = true;
        _foodsGrid.RowHeadersVisible = false;
        _foodsGrid.MultiSelect = false;
        _foodsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _foodsGrid.SelectionChanged += FoodsGrid_SelectionChanged;
        ConfigureFoodsGrid();

        var foodPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };
        foodPanel.Controls.Add(_foodsGrid);
        foodPanel.Controls.Add(_documentIdLabel);
        foodPanel.Controls.Add(foodActionsPanel);
        foodPanel.Controls.Add(foodInputLayout);
        foodsPage.Controls.Add(foodPanel);

        _usersRefreshButton.Text = "Refresh users";
        _usersRefreshButton.Enabled = false;
        _usersRefreshButton.Click += UsersRefreshButton_Click;

        _userAddButton.Text = "Add user";
        _userAddButton.Click += AddUserButton_Click;

        _userUpdateButton.Text = "Update";
        _userUpdateButton.Enabled = false;
        _userUpdateButton.Click += UpdateUserButton_Click;

        _userDeleteButton.Text = "Delete";
        _userDeleteButton.Enabled = false;
        _userDeleteButton.Click += DeleteUserButton_Click;

        _userClearButton.Text = "Clear";
        _userClearButton.Click += ClearUserButton_Click;

        var usernameLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Username:"
        };
        var userEmailLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Email:"
        };
        foreach (var label in new[] { usernameLabel, userEmailLabel })
        {
            label.AutoSize = false;
            label.Margin = new Padding(0);
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        _userUsernameTextBox.Dock = DockStyle.Fill;
        _userUsernameTextBox.Margin = new Padding(3, 5, 3, 5);
        _userUsernameTextBox.MaxLength = 100;
        _userEmailTextBox.Dock = DockStyle.Fill;
        _userEmailTextBox.Margin = new Padding(3, 5, 3, 5);
        _userEmailTextBox.MaxLength = 254;

        var userEditorLayout = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Top,
            Height = 40,
            Margin = new Padding(0),
            RowCount = 1
        };
        userEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        userEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        userEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        userEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        userEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        userEditorLayout.Controls.Add(usernameLabel, 0, 0);
        userEditorLayout.Controls.Add(_userUsernameTextBox, 1, 0);
        userEditorLayout.Controls.Add(userEmailLabel, 2, 0);
        userEditorLayout.Controls.Add(_userEmailTextBox, 3, 0);

        var userActionsPanel = CreateButtonPanel(
            _userAddButton,
            _userUpdateButton,
            _userDeleteButton,
            _userClearButton,
            _usersRefreshButton);
        userActionsPanel.Dock = DockStyle.Top;
        userActionsPanel.Height = 40;

        _userDocumentIdLabel.AutoSize = false;
        _userDocumentIdLabel.Dock = DockStyle.Top;
        _userDocumentIdLabel.Height = 30;
        _userDocumentIdLabel.Margin = new Padding(0);
        _userDocumentIdLabel.Text = "User document: -";
        _userDocumentIdLabel.TextAlign = ContentAlignment.MiddleLeft;

        ConfigureUsersGrid();
        _usersGrid.Dock = DockStyle.Fill;
        _usersGrid.SelectionChanged += UsersGrid_SelectionChanged;

        var usersPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };
        usersPanel.Controls.Add(_usersGrid);
        usersPanel.Controls.Add(_userDocumentIdLabel);
        usersPanel.Controls.Add(userActionsPanel);
        usersPanel.Controls.Add(userEditorLayout);
        usersPage.Controls.Add(usersPanel);

        _ordersRefreshButton.Text = "Refresh orders";
        _ordersRefreshButton.Enabled = false;
        _ordersRefreshButton.Click += OrdersRefreshButton_Click;

        _orderAddButton.Text = "Add order";
        _orderAddButton.Click += AddOrderButton_Click;

        _orderUpdateButton.Text = "Update";
        _orderUpdateButton.Enabled = false;
        _orderUpdateButton.Click += UpdateOrderButton_Click;

        _orderDeleteButton.Text = "Delete";
        _orderDeleteButton.Enabled = false;
        _orderDeleteButton.Click += DeleteOrderButton_Click;

        _orderClearButton.Text = "Clear";
        _orderClearButton.Click += ClearOrderButton_Click;

        var orderCustomerLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Customer:"
        };
        var orderFoodLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Food:"
        };
        var orderQuantityLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = "Quantity:"
        };
        foreach (var label in new[] { orderCustomerLabel, orderFoodLabel, orderQuantityLabel })
        {
            label.AutoSize = false;
            label.Margin = new Padding(0);
            label.TextAlign = ContentAlignment.MiddleLeft;
        }

        foreach (var comboBox in new[] { _orderUserComboBox, _orderFoodComboBox })
        {
            comboBox.Dock = DockStyle.Fill;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.FlatStyle = FlatStyle.Standard;
            comboBox.Margin = new Padding(3, 5, 3, 5);
        }
        _orderUserComboBox.DisplayMember = nameof(UserRow.DisplayName);
        _orderFoodComboBox.DisplayMember = nameof(FoodRow.DisplayName);
        _orderFoodComboBox.SelectedIndexChanged += OrderFoodComboBox_SelectedIndexChanged;

        _orderQuantityInput.Dock = DockStyle.Fill;
        _orderQuantityInput.Margin = new Padding(3, 5, 3, 5);
        _orderQuantityInput.Minimum = 1;
        _orderQuantityInput.Maximum = 100_000;
        _orderQuantityInput.ThousandsSeparator = true;
        _orderQuantityInput.Value = 1;
        _orderQuantityInput.ValueChanged += OrderQuantityInput_ValueChanged;

        _orderSummaryLabel.AutoSize = false;
        _orderSummaryLabel.Dock = DockStyle.Fill;
        _orderSummaryLabel.Margin = new Padding(3, 5, 3, 5);
        _orderSummaryLabel.Text = "Unit price: $0.00 | Order total: $0.00";
        _orderSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;

        var orderEditorLayout = new TableLayoutPanel
        {
            ColumnCount = 4,
            Dock = DockStyle.Top,
            Height = 80,
            Margin = new Padding(0),
            RowCount = 2
        };
        orderEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));
        orderEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        orderEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 65));
        orderEditorLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        orderEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        orderEditorLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        orderEditorLayout.Controls.Add(orderCustomerLabel, 0, 0);
        orderEditorLayout.Controls.Add(_orderUserComboBox, 1, 0);
        orderEditorLayout.Controls.Add(orderFoodLabel, 2, 0);
        orderEditorLayout.Controls.Add(_orderFoodComboBox, 3, 0);
        orderEditorLayout.Controls.Add(orderQuantityLabel, 0, 1);
        orderEditorLayout.Controls.Add(_orderQuantityInput, 1, 1);
        orderEditorLayout.Controls.Add(_orderSummaryLabel, 2, 1);
        orderEditorLayout.SetColumnSpan(_orderSummaryLabel, 2);

        var orderActionsPanel = CreateButtonPanel(
            _orderAddButton,
            _orderUpdateButton,
            _orderDeleteButton,
            _orderClearButton,
            _ordersRefreshButton);
        orderActionsPanel.Dock = DockStyle.Top;
        orderActionsPanel.Height = 40;

        _orderDocumentIdLabel.AutoSize = false;
        _orderDocumentIdLabel.Dock = DockStyle.Top;
        _orderDocumentIdLabel.Height = 30;
        _orderDocumentIdLabel.Margin = new Padding(0);
        _orderDocumentIdLabel.Text = "Order document: -";
        _orderDocumentIdLabel.TextAlign = ContentAlignment.MiddleLeft;

        _salesSummaryLabel.AutoSize = false;
        _salesSummaryLabel.Dock = DockStyle.Top;
        _salesSummaryLabel.Height = 30;
        _salesSummaryLabel.Margin = new Padding(0);
        _salesSummaryLabel.Text = "Total sales: $0.00 | Orders: 0";
        _salesSummaryLabel.TextAlign = ContentAlignment.MiddleLeft;

        ConfigureOrdersGrid();
        _ordersGrid.Dock = DockStyle.Fill;
        _ordersGrid.SelectionChanged += OrdersGrid_SelectionChanged;

        var ordersPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15)
        };
        ordersPanel.Controls.Add(_ordersGrid);
        ordersPanel.Controls.Add(_salesSummaryLabel);
        ordersPanel.Controls.Add(_orderDocumentIdLabel);
        ordersPanel.Controls.Add(orderActionsPanel);
        ordersPanel.Controls.Add(orderEditorLayout);
        ordersPage.Controls.Add(ordersPanel);

        mainTabs.TabPages.Add(foodsPage);
        mainTabs.TabPages.Add(usersPage);
        mainTabs.TabPages.Add(ordersPage);
        mainTabs.SelectedIndexChanged += async (_, _) =>
        {
            _ordersTabIsSelected = mainTabs.SelectedTab == ordersPage;

            if (mainTabs.SelectedTab == usersPage && !string.IsNullOrWhiteSpace(_firebaseIdToken))
            {
                await LoadUsersAsync();
            }
            else if (mainTabs.SelectedTab == ordersPage && !string.IsNullOrWhiteSpace(_firebaseIdToken))
            {
                await LoadFoodsAsync();
                await LoadUsersAsync();
                await LoadOrdersAsync();
            }
        };

        Controls.AddRange(
        [
            titleLabel,
            authLayout,
            _statusLabel,
            mainTabs
        ]);
    }

}
