# 🏡 Jaahub

Jaahub is a **real estate platform** (similar to Realtor but simpler) built with **ASP.NET Core Web API** for the backend and **React** for the frontend.  
It provides a complete solution for property management, rentals, transactions, and user interactions.

---

## ✨ Features

- 🔑 **Authentication & Authorization**
  - User registration & login with JWT
  - User profile management (`/api/profile`)
  - Role-based access

- 🏠 **Property Management**
  - Add / Edit / Delete properties
  - Categorize properties
  - Upload property images
  - Track property views

- 🤝 **User Interactions**
  - Add to favorites
  - Send & receive messages
  - Write & read property reviews

- 📑 **Rentals & Transactions**
  - Create and manage rental contracts
  - Handle financial transactions
  - Calculate rental total price

- 🖼 **File Upload**
  - Upload images via `multipart/form-data`
  - Access uploaded images through static URLs

---

## 🛠️ Tech Stack

**Backend**
- ASP.NET Core Web API
- Entity Framework Core
- JWT Authentication
- Swagger / OpenAPI for documentation

**Frontend**
- React (with modern hooks & components)
- Axios for API calls
- TailwindCSS / custom styling

**Database**
- SQL Server (Entity Framework migrations)

---

## 🚀 Getting Started

### 1️⃣ Clone the repository
```bash
git clone https://github.com/Phoenix15049/Jaahub.git
cd Jaahub
```

### 2️⃣ Backend (ASP.NET Core API)
1. Navigate to the backend project:
   ```bash
   cd Jaahub.Api
   ```
2. Update the database (EF Core migrations):
   ```bash
   dotnet ef database update
   ```
3. Run the API:
   ```bash
   dotnet run
   ```
4. API will be available at:  
   👉 `https://localhost:7146`

### 3️⃣ Frontend (React)
1. Navigate to frontend folder (e.g., `jaahub-client`):
   ```bash
   cd jaahub-client
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Run the development server:
   ```bash
   npm start
   ```
4. Frontend will be available at:  
   👉 `http://localhost:3000`

---

## 📖 API Documentation
Full API documentation is available via Swagger:  
👉 `https://localhost:7146/swagger`

---

## 📸 Screenshots (Optional)
> Add screenshots here if you have them, e.g.:

![Properties Page](docs/screenshots/properties.png)
![Profile Page](docs/screenshots/profile.png)

---

## 👨‍💻 Development Notes
- Clean architecture with DTOs to separate input/output from database entities
- AutoMapper or manual mapping between DTOs and entities
- Secure endpoints with `[Authorize]` attribute
- Form-data upload support with custom size limits
- Easily extensible for more features in the future

---

## 📬 Contact
If you have any questions, feel free to reach out:  
**GitHub**: [Phoenix15049](https://github.com/Phoenix15049)

---
