using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MyFirstapp;

public partial class Form1
{
    private async void LoginButton_Click(object? sender, EventArgs e)
    {
        if (!IsFirebaseConfigured())
        {
            SetStatus("Set FirebaseProjectId and FirebaseWebApiKey in Form1.cs first.", Color.DarkOrange);
            return;
        }

        var email = _emailTextBox.Text.Trim();
        var password = _passwordTextBox.Text;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            SetStatus("Enter the Firebase admin email and password.", Color.Firebrick);
            return;
        }

        try
        {
            SetStatus("Signing in...", Color.DarkOrange);
            _loginButton.Enabled = false;

            var endpoint =
                $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={Uri.EscapeDataString(FirebaseWebApiKey)}";

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
                SetStatus($"Sign-in failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(responseBody);
            _firebaseIdToken = authResponse?.IdToken;

            if (string.IsNullOrWhiteSpace(_firebaseIdToken))
            {
                SetStatus("Firebase did not return an ID token.", Color.Firebrick);
                return;
            }

            SetFoodActionsEnabled(true);
            _emailTextBox.Enabled = false;
            _passwordTextBox.Enabled = false;
            _loginButton.Enabled = false;
            _registerButton.Enabled = false;
            SetStatus("Signed in. You can now save foods.", Color.DarkGreen);

            await LoadFoodsAsync();
            await LoadUsersAsync();
            await LoadOrdersAsync();
            StartOrdersPolling();
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Network error: {ex.Message}", Color.Firebrick);
        }
        finally
        {
            if (_firebaseIdToken is null)
            {
                _loginButton.Enabled = true;
                _registerButton.Enabled = true;
            }
        }
    }

    private async void RegisterButton_Click(object? sender, EventArgs e)
    {
        if (!IsFirebaseConfigured())
        {
            SetStatus("Set FirebaseProjectId and FirebaseWebApiKey in Form1.cs first.", Color.DarkOrange);
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

        try
        {
            _loginButton.Enabled = false;
            _registerButton.Enabled = false;
            SetStatus("Creating Firebase account...", Color.DarkOrange);

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
                SetStatus($"Registration failed: {GetFirebaseError(responseBody)}", Color.Firebrick);
                return;
            }

            var authResponse = JsonSerializer.Deserialize<FirebaseAuthResponse>(responseBody);
            if (string.IsNullOrWhiteSpace(authResponse?.IdToken) ||
                string.IsNullOrWhiteSpace(authResponse.LocalId))
            {
                SetStatus("Firebase did not return the new user's ID.", Color.Firebrick);
                return;
            }

            _firebaseIdToken = authResponse.IdToken;
            _emailTextBox.Text = email;
            _passwordTextBox.Text = password;

            var profileSaved = await SaveUserProfileAsync(
                authResponse.LocalId,
                username,
                email);

            SetFoodActionsEnabled(true);
            _emailTextBox.Enabled = false;
            _passwordTextBox.Enabled = false;
            _loginButton.Enabled = false;
            _registerButton.Enabled = false;
            if (profileSaved)
            {
                SetStatus("Account created and saved to the users collection.", Color.DarkGreen);
            }

            await LoadFoodsAsync();
            await LoadUsersAsync();
            await LoadOrdersAsync();
            StartOrdersPolling();
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
            if (_firebaseIdToken is null)
            {
                _loginButton.Enabled = true;
                _registerButton.Enabled = true;
            }
        }
    }


    private async Task<bool> SaveUserProfileAsync(
        string userId,
        string username,
        string email)
    {
        var endpoint =
            $"{GetUserDocumentEndpoint(userId)}?updateMask.fieldPaths=uid&updateMask.fieldPaths=username&updateMask.fieldPaths=email&updateMask.fieldPaths=createdAt&updateMask.fieldPaths=password";

        // Never put the password in Firestore. Firebase Authentication owns it.
        var requestBody = new
        {
            fields = new Dictionary<string, object>
            {
                ["uid"] = new { stringValue = userId },
                ["username"] = new { stringValue = username },
                ["email"] = new { stringValue = email },
                ["createdAt"] = new
                {
                    timestampValue = DateTime.UtcNow.ToString("O")
                }
            }
        };

        try
        {
            using var request = CreateFirestoreRequest(
                HttpMethod.Patch,
                endpoint,
                requestBody);
            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                SetStatus(
                    $"Account created, but the users profile was not saved: {GetFirebaseError(responseBody)}",
                    Color.Firebrick);
                return false;
            }

            return true;
        }
        catch (HttpRequestException ex)
        {
            SetStatus($"Account created, but profile save failed: {ex.Message}", Color.Firebrick);
            return false;
        }
    }

    private bool EnsureSignedIn()
    {
        if (!string.IsNullOrWhiteSpace(_firebaseIdToken))
        {
            return true;
        }

        SetStatus("Sign in before managing food records.", Color.Firebrick);
        return false;
    }

}
