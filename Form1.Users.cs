using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private async void AddUserButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn())
        {
            return;
        }

        using var registrationDialog = new RegistrationDialog();
        if (registrationDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var username = registrationDialog.Username;
        var email = registrationDialog.Email;
        var password = registrationDialog.Password;
        var previousToken = _firebaseIdToken;

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Creating user account...", Color.DarkOrange);

            var endpoint =
                $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={Uri.EscapeDataString(FirebaseWebApiKey)}";
            using var response = await _httpClient.PostAsJsonAsync(
                endpoint,
                new
                {
                    email,
                    password,
                    returnSecureToken = true
                });
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"User creation failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(responseBody);
            if (string.IsNullOrWhiteSpace(authResponse?.IdToken) ||
                string.IsNullOrWhiteSpace(authResponse.LocalId))
            {
                SetStatus("Firebase did not return the new user's ID.", Color.Firebrick);
                return;
            }

            // Use the new user's token only for creating its own profile, then
            // restore the currently signed-in admin token.
            _firebaseIdToken = authResponse.IdToken;
            var profileSaved = await SaveUserProfileAsync(
                authResponse.LocalId,
                username,
                email);
            _firebaseIdToken = previousToken;

            if (profileSaved)
            {
                SetStatus("User account and profile created.", Color.DarkGreen);
            }

            await LoadUsersAsync();
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
            _firebaseIdToken = previousToken;
            SetFoodActionsEnabled(true);
        }
    }

    private async void UpdateUserButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() || string.IsNullOrWhiteSpace(_selectedUserDocumentId) ||
            !TryReadUserInput(out var username, out var email))
        {
            return;
        }

        var documentId = _selectedUserDocumentId;

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Updating user profile...", Color.DarkOrange);

            var endpoint =
                $"{GetUserDocumentEndpoint(documentId)}?updateMask.fieldPaths=username&updateMask.fieldPaths=email&updateMask.fieldPaths=password";
            using var request = CreateFirestoreRequest(
                HttpMethod.Patch,
                endpoint,
                new
                {
                    fields = new Dictionary<string, object>
                    {
                        ["username"] = new { stringValue = username },
                        ["email"] = new { stringValue = email }
                    }
                });
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"User update failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            ClearUserEditor();
            SetStatus("User profile updated. Firebase Auth email was not changed.", Color.DarkGreen);
            await LoadUsersAsync();
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

    private async void DeleteUserButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn() || string.IsNullOrWhiteSpace(_selectedUserDocumentId))
        {
            SetStatus("Select a user profile before deleting.", Color.Firebrick);
            return;
        }

        var documentId = _selectedUserDocumentId;
        var answer = MessageBox.Show(
            this,
            "Delete this Firestore user profile? The Firebase Authentication account will remain.",
            "Confirm profile delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (answer != DialogResult.Yes)
        {
            return;
        }

        try
        {
            SetFoodActionsEnabled(false);
            SetStatus("Deleting user profile...", Color.DarkOrange);

            using var request = CreateFirestoreRequest(
                HttpMethod.Delete,
                GetUserDocumentEndpoint(documentId));
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"User delete failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            ClearUserEditor();
            SetStatus("User profile deleted. Firebase Auth account was not deleted.", Color.DarkGreen);
            await LoadUsersAsync();
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

    private void ClearUserButton_Click(object? sender, EventArgs e)
    {
        ClearUserEditor();
        SetStatus("Ready to manage users.", Color.DarkOrange);
    }

    private void UsersGrid_SelectionChanged(object? sender, EventArgs e)
    {
        if (_usersGrid.SelectedRows.Count == 0 ||
            _usersGrid.SelectedRows[0].DataBoundItem is not UserRow user)
        {
            _selectedUserDocumentId = null;
            UpdateActionButtons();
            return;
        }

        _selectedUserDocumentId = user.DocumentId;
        _userUsernameTextBox.Text = user.Username;
        _userEmailTextBox.Text = user.Email;
        _userDocumentIdLabel.Text = $"User document: {user.DocumentId}";
        UpdateActionButtons();
    }

    private void ClearUserEditor()
    {
        _selectedUserDocumentId = null;
        _userUsernameTextBox.Clear();
        _userEmailTextBox.Clear();
        _userDocumentIdLabel.Text = "User document: -";

        if (_usersGrid.Rows.Count > 0)
        {
            _usersGrid.CurrentCell = null;
            _usersGrid.ClearSelection();
        }

        UpdateActionButtons();
    }

    private bool TryReadUserInput(out string username, out string email)
    {
        username = _userUsernameTextBox.Text.Trim();
        email = _userEmailTextBox.Text.Trim();

        if (username.Length < 2)
        {
            SetStatus("Username must contain at least 2 characters.", Color.Firebrick);
            _userUsernameTextBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            SetStatus("Enter a valid user email.", Color.Firebrick);
            _userEmailTextBox.Focus();
            return false;
        }

        _userUsernameTextBox.Text = username;
        _userEmailTextBox.Text = email;
        return true;
    }

    private async void UsersRefreshButton_Click(object? sender, EventArgs e)
    {
        if (!EnsureSignedIn())
        {
            return;
        }

        SetStatus("Refreshing users...", Color.DarkOrange);
        if (await LoadUsersAsync())
        {
            SetStatus("User accounts refreshed.", Color.DarkGreen);
        }
    }


    private async Task<bool> LoadUsersAsync()
    {
        if (string.IsNullOrWhiteSpace(_firebaseIdToken))
        {
            return false;
        }

        try
        {
            var endpoint = $"{GetUsersEndpoint()}?pageSize=100";
            using var request = CreateFirestoreRequest(HttpMethod.Get, endpoint);
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus($"Could not load users: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return false;
            }

            using var document = JsonDocument.Parse(responseBody);
            var users = new List<UserRow>();

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
                    var uid = ReadStringField(fields, "uid");

                    users.Add(
                        new UserRow
                        {
                            DocumentId = documentId,
                            Uid = string.IsNullOrWhiteSpace(uid) ? documentId : uid,
                            Username = ReadStringField(fields, "username"),
                            Email = ReadStringField(fields, "email"),
                            CreatedAt = ReadTimestampField(fields, "createdAt")
                        });
                }
            }

            _userRows = users
                .OrderBy(user => user.Username, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(user => user.Email, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            _usersGrid.DataSource = _userRows;
            BindOrderUserOptions(_userRows);
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

}
