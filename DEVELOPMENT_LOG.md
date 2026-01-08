# Development Log

## Phase 1: Project Setup
- Verified project structure (React Frontend, .NET API, PHP Service).
- Verified Database Schema (`DB_Schema.sql`).

## Phase 2: Database & Authentication (Backend)

### 1. Database Configuration
- Verified `DB_Schema.sql` structure.
- Updated `users` table in `DB_Schema.sql` to include `password_hash` for local authentication.
- Configured `.NET API` to use MySQL (Pomelo.EntityFrameworkCore.MySql).

### 2. Authentication Implementation (.NET API)

#### Models & DTOs
- Updated `User` model to include `PasswordHash` property.
- Created `RegisterDto` (Name, Email, Password).
- Created `LoginDto` (Email, Password).
- Created `AuthResponseDto` (Token, RefreshToken).

#### Services
- Created `IAuthService` interface with `RegisterAsync` and `LoginAsync` methods.
- Implemented `AuthService`:
  - **RegisterAsync**: 
    - Validates email uniqueness
    - Hashes password using BCrypt (work factor: default)
    - Saves user to database
    - Generates JWT token
  - **LoginAsync**:
    - Finds user by email
    - Verifies password using BCrypt
    - Generates JWT token
  - **CreateToken** (private):
    - Creates JWT with claims (UserId, Username, Email)
    - Uses HMAC-SHA512 signature
    - Token expiry: 1 day

#### Controllers
- Created `AuthController` with two endpoints:
  - `POST /api/auth/register` - User registration
  - `POST /api/auth/login` - User login
- Added error handling with try-catch blocks

#### Configuration
- Added JWT Authentication in `Program.cs`:
  - Configured `JwtBearerDefaults` authentication scheme
  - Set up token validation parameters
  - Registered `IAuthService` as scoped service
  - Added `UseAuthentication()` and `UseAuthorization()` middleware
- Added `JwtSettings` section to `appsettings.json` with secret key

### 3. Technical Details
- **Password Hashing**: BCrypt.Net-Next (v4.0.3)
- **JWT Generation**: System.IdentityModel.Tokens.Jwt (v8.2.1)
- **Database**: Entity Framework Core 9.0 with MySQL (Pomelo.EntityFrameworkCore.MySql v9.0.0)
- **Security**: Passwords never stored in plain text, JWT tokens for stateless authentication

### 4. Testing
- Installed `dotnet-ef` tool globally (v9.0.0)
- Created migration: `AddPasswordHashToUser`
- Applied migration to database
- Tested registration endpoint: ✅ Success
- Tested login endpoint: ✅ Success
- Verified JWT token generation

## Phase 3: React Frontend

### 1. Setup & Dependencies
- Installed dependencies:
  - `react-router-dom` - Client-side routing
  - `axios` - HTTP client
  - `lucide-react` - Icon library
  - `tailwindcss` - CSS framework

### 2. Project Structure
Created folder structure:
```
src/
├── pages/          # Page components
├── components/     # Reusable components
├── services/       # API services
└── context/        # React context providers
```

### 3. Configuration
- Configured Tailwind CSS with custom primary color palette
- Updated `index.css` with Tailwind directives

### 4. Services Layer
- Created `api.js`:
  - Axios instance with base URL configuration
  - Request interceptor to attach JWT token
  - `authService` with `register()` and `login()` methods

### 5. State Management
- Created `AuthContext`:
  - Manages authentication state (user, token, loading)
  - Provides `login()` and `logout()` methods
  - Persists token in localStorage
  - Exposes `isAuthenticated` boolean

### 6. Pages
- **Login Page** (`/login`):
  - Email and password form
  - Error handling with visual feedback
  - Loading states
  - Link to registration
  - Redirects to dashboard on success
  
- **Register Page** (`/register`):
  - Name, email, password, confirm password fields
  - Client-side validation (password match, minimum length)
  - Error handling
  - Link to login
  - Auto-login after registration

- **Dashboard Page** (`/dashboard`):
  - Protected route (requires authentication)
  - Logout functionality
  - Placeholder for future features

### 7. Components
- **ProtectedRoute**:
  - Route guard component
  - Redirects to login if not authenticated
  - Shows loading state while checking auth

### 8. Routing
- Updated `App.js` with React Router:
  - `/login` - Login page
  - `/register` - Register page
  - `/dashboard` - Protected dashboard
  - `/` - Redirects to login

### 9. Design
- Gradient backgrounds (blue to indigo)
- Card-based layouts with shadows
- Consistent color scheme using Tailwind
- Icons from Lucide React
- Responsive design
- Accessible form inputs with labels

## Next Steps
- ~~Test the complete authentication flow~~ ✅ **COMPLETED**
- Add GitHub OAuth integration
- Build repository management features
- Implement PR monitoring
- Create code review visualization components

---

## Phase 4: CORS Configuration & Testing

### 1. CORS Setup (.NET API)
**Problem**: React app (localhost:3000) couldn't communicate with API (localhost:5116) due to browser CORS policy.

**Solution**: Added CORS configuration in `Program.cs`:
```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        });
});

// In middleware pipeline:
app.UseCors("AllowReactApp");
```

**Why CORS is needed**:
- Browsers enforce Same-Origin Policy for security
- Different ports = different origins
- CORS headers tell the browser it's safe to allow cross-origin requests

**Security Note**: In production, replace `http://localhost:3000` with your actual frontend domain.

### 2. End-to-End Testing

**Test Results**: ✅ All tests passed

**Registration Flow**:
1. User fills registration form (Name, Email, Password)
2. Frontend validates password match and length
3. POST request to `/api/auth/register`
4. Backend validates email uniqueness
5. Password hashed with BCrypt
6. User saved to database
7. JWT token generated and returned
8. Token stored in localStorage
9. User redirected to dashboard

**Login Flow**:
1. User enters email and password
2. POST request to `/api/auth/login`
3. Backend finds user by email
4. Password verified with BCrypt
5. JWT token generated
6. Token stored in localStorage
7. User redirected to dashboard

**Protected Routes**:
1. Dashboard requires authentication
2. `ProtectedRoute` component checks for token
3. Redirects to login if not authenticated
4. Allows access if token exists

**Logout Flow**:
1. User clicks logout button
2. Token removed from localStorage
3. Auth state cleared
4. User redirected to login page

### 3. Verified Components

**Frontend**:
- ✅ Registration page with validation
- ✅ Login page with error handling
- ✅ Protected dashboard
- ✅ Route guards
- ✅ Token persistence
- ✅ Logout functionality

**Backend**:
- ✅ User registration endpoint
- ✅ Login endpoint
- ✅ JWT token generation
- ✅ Password hashing (BCrypt)
- ✅ Email uniqueness validation
- ✅ CORS configuration

### 4. Current Application State

**Database**: MySQL with 7 tables (users table includes password_hash)

**Running Services**:
- React Frontend: http://localhost:3000
- .NET API: http://localhost:5116
- MySQL: localhost:3306

**Authentication**: Fully functional with JWT tokens

---

## Phase 5: Core Analysis Logic (Backend MVP)

### 1. Orchestration Service (.NET)
- Implemented `ReviewController` to expose `POST /api/review/analyze`.
- Implemented `ReviewService`:
  - **Fetching Code**: Uses `Octokit` to fetch PR files and diffs from GitHub (Anonymous mode).
  - **Payload Transformation**: Converts Octokit models into a simpler format `{ path, content }` for the analysis engine.
  - **PHP Integration**: Sends code to the PHP service via HTTP.
  - **Result Handling**: Parses the complex JSON response from PHP and flattens it into a simple `AnalysisResultDto` for the frontend.
  - **Configuration**: Dynamic service URLs via `appsettings.json` (`ServiceUrls:PhpAnalysisApi`).
  - **Resilience**: Implemented `IHttpClientFactory` to manage HTTP connections efficiently.

### 2. Analysis Engine (PHP)
- **Framework**: Slim 4 Microservice.
- **Endpoint**: `POST /api/analyze/files`.
- **Logic**: Accepts a list of files, runs specific analyzers (Complexity, Security, Style), and returns a detailed JSON report including a quality score (0-100).
- **Status**: The service is fully functional and responding to the .NET API.

### 3. Simplified MVP Flow
- We decided to skip saving results to the database for this specific "Check my PR" feature to keep the MVP simple.
- **Flow**: React -> .NET API -> GitHub -> PHP Service -> .NET API -> React.
- **Benefit**: Immediate feedback without complex state management.

### 4. Technical Improvements
- **.NET 9.0**: Upgraded project validation to .NET 9.0.
- **Dependency Injection**: Registered `IReviewService` and `IHttpClientFactory`.
- **Config**: Extracted hardcoded URLs to `appsettings.json`.

---

## Phase 6: GitHub OAuth Integration (Next)

### Objectives
1. Register OAuth App on GitHub
2. Implement OAuth flow in .NET API
3. Add "Sign in with GitHub" button to React
4. Store GitHub access tokens
5. Link GitHub accounts to existing users

### Required Changes

**Backend**:
- Install `AspNet.Security.OAuth.GitHub` package
- Add GitHub OAuth configuration
- Create `/api/auth/github` callback endpoint
- Store `github_access_token` in users table
- Implement GitHub API client

**Frontend**:
- Add GitHub OAuth button
- Handle OAuth redirect
- Store GitHub user data

**Database**:
- Add `github_access_token` column to users table (encrypted)
- Add `github_username` column

### GitHub OAuth Flow
1. User clicks "Sign in with GitHub"
2. Redirect to GitHub authorization page
3. User approves access
4. GitHub redirects back with code
5. Exchange code for access token
6. Fetch user info from GitHub API
7. Create or link user account
8. Generate JWT token
9. Redirect to dashboard

