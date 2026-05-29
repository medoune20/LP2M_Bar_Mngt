const modules = [
  { key: "dashboard", icon: "dashboard", title: "Tableau de bord", subtitle: "Vue globale de l'activite locale du bar." },
  { key: "cash", icon: "cash", title: "Caisse", subtitle: "Ouverture, cloture et suivi des sessions de caisse." },
  { key: "sales", icon: "sales", title: "Ventes", subtitle: "Tickets, encaissements et annulations controlees." },
  { key: "products", icon: "products", title: "Produits", subtitle: "Catalogue, categories, prix et seuils." },
  { key: "stock", icon: "stock", title: "Stock", subtitle: "Ajustements, entrees et alertes stock faible." },
  { key: "expenses", icon: "expenses", title: "Depenses", subtitle: "Charges, achats et paiements depuis caisse." },
  { key: "reports", icon: "reports", title: "Rapports", subtitle: "Synthese journaliere et exports CSV." },
  { key: "users", icon: "users", title: "Utilisateurs", subtitle: "Comptes, roles, securite et audit." },
  { key: "settings", icon: "settings", title: "Parametres", subtitle: "Profil du bar, tickets, logo et personnalisation." }
];

let state = {};
let currentModule = "dashboard";
let currentUser = null;
let pendingTwoFactorChallenge = null;
let saleCart = [];
let showHidden = false;
let productView = "products";
let expenseView = "expenses";
let selectedTargets = new Set();
let productImageData = null;
let categoryImageData = null;
let profileLogoData = null;
let profileCoverData = null;
let qrStream = null;
let qrTimer = null;
let photoStream = null;
let photoTarget = null;
let tableSearchTerm = "";
let tableFilterMode = "all";
let currentTablePage = 1;

const $ = (id) => document.getElementById(id);
const moneyFormatter = new Intl.NumberFormat("fr-CI", { style: "currency", currency: "XOF", maximumFractionDigits: 0 });
const money = (value) => moneyFormatter.format(Number(value ?? 0));
const date = (value) => value ? new Date(value).toLocaleString("fr-FR") : "";
const todayLabel = () => new Date().toLocaleDateString("fr-FR", { weekday: "long", day: "2-digit", month: "long", year: "numeric" });

function initializeTheme() {
  const saved = localStorage.getItem("lp2m-theme") || "light";
  document.body.dataset.theme = saved;
  syncThemeButton();
}

function toggleTheme() {
  const nextTheme = document.body.dataset.theme === "dark" ? "light" : "dark";
  document.body.dataset.theme = nextTheme;
  localStorage.setItem("lp2m-theme", nextTheme);
  syncThemeButton();
}

function syncThemeButton() {
  const isDark = document.body.dataset.theme === "dark";
  $("themeToggleBtn").textContent = isDark ? "Theme clair" : "Theme sombre";
  $("themeToggleBtn").setAttribute("aria-pressed", String(isDark));
}

function updateCurrentDate() {
  $("currentDate").textContent = todayLabel();
}

document.addEventListener("DOMContentLoaded", async () => {
  initializeTheme();
  updateCurrentDate();
  renderNav();
  bindForms();
  bindAuth();
  document.querySelectorAll("[data-close]").forEach((button) => {
    button.addEventListener("click", () => {
      if (button.dataset.close === "qrModal") {
        stopQrScanner();
      }
      if (button.dataset.close === "photoModal") {
        stopPhotoCamera();
      }
      $(button.dataset.close).close();
    });
  });
  $("refreshBtn").addEventListener("click", () => loadData("Donnees actualisees."));
  $("bulkHideBtn").addEventListener("click", () => bulkSetHidden(true));
  $("bulkShowBtn").addEventListener("click", () => bulkSetHidden(false));
  $("bulkDeleteBtn").addEventListener("click", bulkDelete);
  $("printTicketBtn").addEventListener("click", () => window.print());
  $("themeToggleBtn").addEventListener("click", toggleTheme);
  $("tableSearch").addEventListener("input", () => {
    tableSearchTerm = $("tableSearch").value;
    currentTablePage = 1;
    applyTableControls();
  });
  $("tableFilter").addEventListener("change", () => {
    tableFilterMode = $("tableFilter").value;
    currentTablePage = 1;
    applyTableControls();
  });
  $("tablePageSize").addEventListener("change", () => {
    currentTablePage = 1;
    applyTableControls();
  });
  $("qrStopBtn").addEventListener("click", stopQrScanner);
  $("qrUseBtn").addEventListener("click", useScannedCode);
  $("saleProduct").addEventListener("change", () => {
    updateSaleCalculator();
    highlightSaleProduct();
  });
  $("saleQuantity").addEventListener("input", updateSaleCalculator);
  $("saleDiscount").addEventListener("input", updateSaleCalculator);
  $("saleAddToCartBtn").addEventListener("click", addSelectedSaleProductToCart);
  $("saleClearCartBtn").addEventListener("click", clearSaleCart);
  $("saleProductSearch").addEventListener("input", renderSaleProductGrid);
  $("saleCategoryFilter").addEventListener("change", renderSaleProductGrid);
  $("photoStopBtn").addEventListener("click", stopPhotoCamera);
  $("photoCaptureBtn").addEventListener("click", capturePhoto);
  $("productPhotoBtn").addEventListener("click", () => openPhotoCamera("product"));
  $("categoryPhotoBtn").addEventListener("click", () => openPhotoCamera("category"));
  $("profileLogoPhotoBtn").addEventListener("click", () => openPhotoCamera("profileLogo"));
  $("profileCoverPhotoBtn").addEventListener("click", () => openPhotoCamera("profileCover"));
  $("copyTwoFactorSecretBtn").addEventListener("click", copyTwoFactorSecret);
  $("productImageFile").addEventListener("change", async () => {
    productImageData = await readImageInput("productImageFile");
    setPreview("productImagePreview", productImageData);
  });
  $("categoryImageFile").addEventListener("change", async () => {
    categoryImageData = await readImageInput("categoryImageFile");
    setPreview("categoryImagePreview", categoryImageData);
  });
  $("profileLogoFile").addEventListener("change", async () => {
    profileLogoData = await readImageInput("profileLogoFile");
    setPreview("profileLogoPreview", profileLogoData);
  });
  $("profileCoverFile").addEventListener("change", async () => {
    profileCoverData = await readImageInput("profileCoverFile");
    setPreview("profileCoverPreview", profileCoverData);
  });
  $("showHidden").addEventListener("change", () => {
    showHidden = $("showHidden").checked;
    renderModule();
    setStatus(showHidden ? "Objets masques affiches." : "Objets masques caches.");
  });
  await initializeAuth();
});

function bindAuth() {
  $("loginForm").addEventListener("submit", login);
  $("loginBackBtn").addEventListener("click", () => setTwoFactorLoginMode(null));
  $("logoutBtn").addEventListener("click", logout);
}

async function initializeAuth() {
  try {
    const session = await requestJson("/api/auth/session");
    if (session.authenticated) {
      await showApp(session, "Application web initialisee.");
      return;
    }
  } catch {
    setLoginStatus("Verification de session impossible.", true);
  }

  showLogin("Connecte-toi pour acceder a LP2M_Bar_Mngt.");
}

async function login(event) {
  event.preventDefault();
  if (pendingTwoFactorChallenge) {
    await verifyTwoFactorLogin();
    return;
  }

  setLoginStatus("Connexion...");
  try {
    const session = await requestJson("/api/auth/login", {
      method: "POST",
      body: {
        username: $("loginUsername").value,
        password: $("loginPassword").value,
        rememberMe: $("loginRemember").checked
      }
    });
    if (session.requiresTwoFactor) {
      setTwoFactorLoginMode(session.challengeId, session.message || "Saisis le code de double authentification.");
      return;
    }

    $("loginPassword").value = "";
    await showApp(session, "Connexion reussie.");
  } catch (error) {
    setLoginStatus(error.message || "Connexion refusee.", true);
  }
}

async function verifyTwoFactorLogin() {
  setLoginStatus("Verification du code...");
  try {
    const session = await requestJson("/api/auth/two-factor", {
      method: "POST",
      body: {
        challengeId: pendingTwoFactorChallenge,
        code: $("loginTwoFactorCode").value
      }
    });
    setTwoFactorLoginMode(null);
    $("loginPassword").value = "";
    $("loginTwoFactorCode").value = "";
    await showApp(session, "Connexion reussie.");
  } catch (error) {
    $("loginTwoFactorCode").value = "";
    setLoginStatus(error.message || "Code invalide.", true);
    $("loginTwoFactorCode").focus();
  }
}

function setTwoFactorLoginMode(challengeId, message = "Saisis le code de double authentification.") {
  pendingTwoFactorChallenge = challengeId;
  const active = Boolean(challengeId);
  $("loginUsername").disabled = active;
  $("loginPassword").disabled = active;
  $("loginRemember").disabled = active;
  $("loginTwoFactorGroup").hidden = !active;
  $("loginTwoFactorCode").required = active;
  $("loginBackBtn").hidden = !active;
  $("loginSubmitBtn").textContent = active ? "Valider le code" : "Se connecter";
  setLoginStatus(active ? message : "Connecte-toi pour acceder a LP2M_Bar_Mngt.");
  if (active) {
    $("loginTwoFactorCode").focus();
  } else {
    $("loginTwoFactorCode").value = "";
    $("loginUsername").disabled = false;
    $("loginPassword").disabled = false;
    $("loginRemember").disabled = false;
  }
}

async function logout() {
  await requestJson("/api/auth/logout", { method: "POST" });
  state = {};
  currentUser = null;
  showLogin("Session fermee.");
}

function showLogin(message) {
  setTwoFactorLoginMode(null, message);
  $("appView").hidden = true;
  $("authView").hidden = false;
  setLoginStatus(message);
  $("loginUsername").focus();
}

async function showApp(session, message) {
  currentUser = session;
  $("currentUser").textContent = `${session.fullName ?? session.username} - ${session.role ?? "Utilisateur"}`;
  $("authView").hidden = true;
  $("appView").hidden = false;
  await loadData(message);
}

function renderNav() {
  $("nav").innerHTML = modules.map((module) =>
    `<button data-module="${module.key}"><span class="nav-icon">${iconSvg(module.icon)}</span><span>${module.title}</span></button>`
  ).join("");

  document.querySelectorAll("#nav button").forEach((button) => {
    button.addEventListener("click", () => selectModule(button.dataset.module));
  });
}

function iconSvg(name) {
  const paths = {
    dashboard: '<path d="M4 13h6V4H4v9Zm10 7h6V4h-6v16ZM4 20h6v-5H4v5Z"/>',
    cash: '<path d="M4 7h16v10H4V7Zm3 3h4m6 0h1M8 17v3m8-3v3M8 4v3m8-3v3"/>',
    sales: '<path d="M5 5h14v14H5V5Zm3 4h8M8 12h8M8 15h5"/>',
    products: '<path d="M4 7 12 3l8 4-8 4-8-4Zm0 4 8 4 8-4M4 15l8 4 8-4"/>',
    stock: '<path d="M5 7h14v12H5V7Zm2-3h10v3H7V4Zm2 7h6"/>',
    expenses: '<path d="M7 4h10v16H7V4Zm3 4h4m-4 4h4m-4 4h2"/>',
    reports: '<path d="M5 19V5h14v14H5Zm3-3V9m4 7v-5m4 5V7"/>',
    users: '<path d="M8 11a3 3 0 1 1 0-6 3 3 0 0 1 0 6Zm8 0a3 3 0 1 1 0-6 3 3 0 0 1 0 6ZM4 20c.5-4 7.5-4 8 0m0 0c.5-4 7.5-4 8 0"/>',
    settings: '<path d="M12 8a4 4 0 1 0 0 8 4 4 0 0 0 0-8Zm0-5v3m0 12v3M4.2 4.2l2.1 2.1m11.4 11.4 2.1 2.1M3 12h3m12 0h3M4.2 19.8l2.1-2.1M17.7 6.3l2.1-2.1"/>'
  };
  return `<svg viewBox="0 0 24 24" aria-hidden="true">${paths[name] ?? paths.dashboard}</svg>`;
}

async function loadData(message) {
  setStatus("Chargement...");
  const data = await api("/api/data");
  if (!data) {
    return;
  }

  state = data;
  selectedTargets.clear();
  applyBusinessProfile();
  renderMetrics();
  renderModule();
  setStatus(message);
}

function applyBusinessProfile() {
  const profile = state.businessProfile ?? {};
  $("brandSigle").textContent = profile.sigle || "LP2M";
  $("brandName").textContent = profile.name || "La pause de Medoune";
}

function selectModule(key) {
  currentModule = key;
  resetTableControls();
  if (key !== "products") {
    productView = "products";
  }
  if (key !== "expenses") {
    expenseView = "expenses";
  }
  renderModule();
  setStatus(`${moduleInfo().title} ouvert.`);
}

function moduleInfo() {
  return modules.find((module) => module.key === currentModule) ?? modules[0];
}

function renderModule() {
  const module = moduleInfo();
  $("pageTitle").textContent = module.title;
  $("pageSubtitle").textContent = module.subtitle;
  document.querySelectorAll("#nav button").forEach((button) => {
    button.classList.toggle("active", button.dataset.module === currentModule);
  });

  renderActions();
  renderTable();
  renderDashboardInsights();
  renderLowStock();
  renderBulkBar();
  updateTableMeta();
  applyTableControls();
}

function renderMetrics() {
  const dashboard = state.dashboard ?? {};
  $("metricRevenue").textContent = money(dashboard.todayRevenue);
  $("metricTickets").textContent = dashboard.todayTicketCount ?? 0;
  $("metricSessions").textContent = dashboard.openCashSessionCount ?? 0;
  $("metricLowStock").textContent = dashboard.lowStockCount ?? 0;
  $("metricExpenses").textContent = money(dashboard.todayExpenseTotal);
  $("metricCashBalance").textContent = money(dashboard.openCashBalance);
}

function renderDashboardInsights() {
  const container = $("dashboardInsights");
  container.hidden = currentModule !== "dashboard";
  if (currentModule !== "dashboard") {
    container.innerHTML = "";
    return;
  }

  const dashboard = state.dashboard ?? {};
  const chart = dashboard.salesChart ?? [];
  const maxAmount = Math.max(...chart.map((point) => Number(point.amount ?? 0)), 1);
  const topProducts = dashboard.topProducts ?? [];
  const lowStock = visibleRows(state.stock).filter((row) => row.isLowStock).slice(0, 5);
  container.innerHTML = `
    <article class="insight-card insight-wide">
      <div class="insight-title">
        <div><h2>Ventes recentes</h2><p>Evolution sur 7 jours</p></div>
        <b>${money(dashboard.estimatedProfit)}</b>
      </div>
      <div class="sales-chart">
        ${chart.map((point) => {
          const height = Math.max(8, Math.round((Number(point.amount ?? 0) / maxAmount) * 112));
          return `<div class="chart-day"><span style="height:${height}px"></span><small>${escapeHtml(point.label)}</small><b>${point.count ?? 0}</b></div>`;
        }).join("")}
      </div>
    </article>
    <article class="insight-card">
      <div class="insight-title"><div><h2>Produits les plus vendus</h2><p>Sur les 30 derniers jours</p></div></div>
      <div class="rank-list">
        ${topProducts.length === 0 ? `<p class="empty-state">Aucune vente produit disponible.</p>` : topProducts.map((item, index) => `
          <div class="rank-line"><span>${index + 1}</span><b>${escapeHtml(item.productName)}</b><small>${item.quantitySold} vendu(s)</small><strong>${money(item.totalAmount)}</strong></div>
        `).join("")}
      </div>
    </article>
    <article class="insight-card">
      <div class="insight-title"><div><h2>Exploitation</h2><p>Caisse, stock et depenses</p></div></div>
      <div class="compact-kpis">
        <div><span>Depenses jour</span><b>${money(dashboard.todayExpenseTotal)}</b></div>
        <div><span>Solde caisse</span><b>${money(dashboard.openCashBalance)}</b></div>
        <div><span>Stock faible</span><b>${dashboard.lowStockCount ?? 0}</b></div>
      </div>
      <div class="rank-list small">
        ${lowStock.length === 0 ? `<p class="empty-state">Aucune alerte stock.</p>` : lowStock.map((item) => `
          <div class="rank-line alert"><b>${escapeHtml(item.productName)}</b><small>${escapeHtml(item.categoryName)}</small><strong>${item.quantity}</strong></div>
        `).join("")}
      </div>
    </article>
  `;
}

function updateTableMeta() {
  const module = moduleInfo();
  $("tableTitle").textContent = currentModule === "dashboard" ? "Synthese operationnelle" : module.title;
  $("tableSubtitle").textContent = currentModule === "reports"
    ? "Rapports, exports et indicateurs consolides."
    : "Donnees consultables, filtrables et exportables depuis SQLite.";
}

function resetTableControls() {
  tableSearchTerm = "";
  tableFilterMode = "all";
  currentTablePage = 1;
  if ($("tableSearch")) {
    $("tableSearch").value = "";
    $("tableFilter").value = "all";
  }
}

function applyTableControls() {
  const tbody = $("dataTable")?.querySelector("tbody");
  if (!tbody) {
    $("tablePager").innerHTML = "";
    return;
  }

  const query = (tableSearchTerm ?? "").trim().toLowerCase();
  const rows = Array.from(tbody.querySelectorAll("tr"));
  const matched = rows.filter((row) => {
    const text = row.textContent.toLowerCase();
    const isHiddenRow = row.classList.contains("hidden-row") || text.includes("masque");
    const isAlert = text.includes("stock faible") || text.includes("inactif") || text.includes("annule") || text.includes("ecart");
    const matchesQuery = !query || text.includes(query);
    const matchesFilter = tableFilterMode === "all"
      || (tableFilterMode === "visible" && !isHiddenRow)
      || (tableFilterMode === "hidden" && isHiddenRow)
      || (tableFilterMode === "alert" && isAlert);
    return matchesQuery && matchesFilter;
  });

  const pageSize = Math.max(1, Number($("tablePageSize").value || 25));
  const totalPages = Math.max(1, Math.ceil(matched.length / pageSize));
  currentTablePage = Math.min(currentTablePage, totalPages);
  const start = (currentTablePage - 1) * pageSize;
  const visibleSet = new Set(matched.slice(start, start + pageSize));
  rows.forEach((row) => {
    row.hidden = !visibleSet.has(row);
  });

  const end = matched.length === 0 ? 0 : Math.min(start + pageSize, matched.length);
  $("tablePager").innerHTML = `
    <span>${matched.length === 0 ? "0 element" : `${start + 1}-${end} sur ${matched.length}`}</span>
    <button type="button" data-page-prev ${currentTablePage <= 1 ? "disabled" : ""}>Precedent</button>
    <button type="button" data-page-next ${currentTablePage >= totalPages ? "disabled" : ""}>Suivant</button>
  `;
  const prev = document.querySelector("[data-page-prev]");
  const next = document.querySelector("[data-page-next]");
  prev?.addEventListener("click", () => {
    currentTablePage = Math.max(1, currentTablePage - 1);
    applyTableControls();
  });
  next?.addEventListener("click", () => {
    currentTablePage = Math.min(totalPages, currentTablePage + 1);
    applyTableControls();
  });
}

function visibleRows(rows) {
  return (rows ?? []).filter((row) => showHidden || !row.isHidden);
}

function hiddenCell(row) {
  return row.isHidden ? `<td>${statusBadge("Masque", "danger")}</td>` : `<td>${statusBadge("Visible", "neutral")}</td>`;
}

function statusBadge(label, tone = "neutral") {
  return `<span class="badge badge-${tone}">${escapeHtml(label)}</span>`;
}

function objectActionButtons(type, id, isHidden) {
  const visibilityButton = isHidden
    ? `<button data-show-object="${type}:${id}">Afficher</button>`
    : `<button data-hide-object="${type}:${id}">Masquer</button>`;
  return `${visibilityButton}<button data-delete-object="${type}:${id}">Supprimer</button>`;
}

function selectHeader() {
  return `<input type="checkbox" data-select-all>`;
}

function selectCell(type, id) {
  const key = `${type}:${id}`;
  return `<td class="select-cell"><input type="checkbox" data-select-object="${key}" ${selectedTargets.has(key) ? "checked" : ""}></td>`;
}

function bindSelectionButtons() {
  document.querySelectorAll("[data-select-object]").forEach((box) => {
    box.addEventListener("change", () => {
      if (box.checked) {
        selectedTargets.add(box.dataset.selectObject);
      } else {
        selectedTargets.delete(box.dataset.selectObject);
      }
      renderBulkBar();
    });
  });

  document.querySelectorAll("[data-select-all]").forEach((box) => {
    box.addEventListener("change", () => {
      document.querySelectorAll("[data-select-object]").forEach((item) => {
        item.checked = box.checked;
        if (box.checked) {
          selectedTargets.add(item.dataset.selectObject);
        } else {
          selectedTargets.delete(item.dataset.selectObject);
        }
      });
      renderBulkBar();
    });
  });
}

function renderBulkBar() {
  $("bulkBar").hidden = selectedTargets.size === 0;
  $("bulkCount").textContent = `${selectedTargets.size} selection(s)`;
}

function bindObjectActionButtons() {
  document.querySelectorAll("[data-hide-object]").forEach((button) => {
    button.addEventListener("click", () => setObjectHidden(button.dataset.hideObject, true));
  });
  document.querySelectorAll("[data-show-object]").forEach((button) => {
    button.addEventListener("click", () => setObjectHidden(button.dataset.showObject, false));
  });
  document.querySelectorAll("[data-delete-object]").forEach((button) => {
    button.addEventListener("click", () => deleteObject(button.dataset.deleteObject));
  });
  bindSelectionButtons();
}

function renderActions() {
  const actions = {
    dashboard: [
      ["Actualiser", "Recharge toutes les donnees de l'application web.", () => loadData("Donnees actualisees.")],
      ["Ouvrir caisse", "Ouvrir une session pour un caissier.", () => openCashOpenModal()],
      ["Verifier stock", "Controle les alertes de stock faible.", () => loadData("Stock verifie.")]
    ],
    cash: [
      ["Ouvrir", "Ouvrir une session de caisse par caissier.", () => openCashOpenModal()],
      ["Cloturer", "Cloturer une session ouverte avec montant declare.", () => openCashCloseModal()],
      ["Actualiser", "Recharger les sessions de caisse.", () => loadData("Caisse actualisee.")]
    ],
    sales: [
      ["Nouvelle vente", "Ouvrir le formulaire de vente.", () => openSaleModal()],
      ["Scanner QR", "Scanner un code pour retrouver un produit.", () => openQrScanner()],
      ["Vente rapide", "Creer une vente sur le premier produit vendable.", () => postAction("/api/sales/quick")],
      ["Reimprimer", "Afficher le dernier ticket personnalise.", () => openTicketModal()],
      ["Annuler", "Annuler la derniere vente validee.", () => postAction("/api/sales/cancel-last")]
    ],
    products: [
      ["Nouveau produit", "Ouvrir le formulaire de creation produit.", () => openProductModal()],
      ["Nouvelle categorie", "Ouvrir le formulaire de categorie.", () => openCategoryModal()],
      ["Scanner QR", "Scanner un code produit ou code-barres.", () => openQrScanner()],
      [productView === "products" ? "Voir categories" : "Voir produits", "Basculer entre catalogue et categories.", () => switchProductView()],
      ["Actualiser", "Recharger le catalogue.", () => loadData("Catalogue actualise.")]
    ],
    stock: [
      ["Ajuster stock", "Ouvrir le formulaire d'ajustement de stock.", () => openStockModal()],
      ["Reapprovisionner", "Reapprovisionner tous les produits sous seuil.", () => postAction("/api/stock/restock-low")],
      ["Actualiser", "Recharger les niveaux de stock.", () => loadData("Stock actualise.")]
    ],
    expenses: [
      ["Nouvelle depense", "Ouvrir le formulaire de depense.", () => openExpenseModal()],
      [expenseView === "expenses" ? "Voir categories" : "Voir depenses", "Basculer entre depenses et categories.", () => switchExpenseView()],
      ["Actualiser", "Recharger les depenses.", () => loadData("Depenses actualisees.")]
    ],
    reports: [
      ["Rapport jour", "Generer la synthese du jour.", () => postAction("/api/reports/daily")],
      ["Export ventes", "Telecharger les ventes en CSV.", () => downloadExport("sales")],
      ["Export produits", "Telecharger les produits en CSV.", () => downloadExport("products")],
      ["Export stock", "Telecharger le stock en CSV.", () => downloadExport("stock")],
      ["Export depenses", "Telecharger les depenses en CSV.", () => downloadExport("expenses")],
      ["Export utilisateurs", "Telecharger les utilisateurs en CSV.", () => downloadExport("users")],
      ["Actualiser", "Recharger les rapports.", () => loadData("Rapports actualises.")]
    ],
    users: [
      ["Nouvel utilisateur", "Ouvrir le formulaire utilisateur.", () => openUserModal()],
      ["Reset admin", "Reinitialiser le mot de passe admin.", () => postAction("/api/users/reset-admin")],
      ["Audit", "Ajouter une trace d'audit de verification.", () => postAction("/api/audit/check")]
    ],
    settings: [
      ["Profil du bar", "Nom, sigle, adresse, contact, logo et ticket.", () => openProfileModal()],
      ["Dernier ticket", "Afficher le dernier ticket personnalise.", () => openTicketModal()],
      ["Actualiser", "Recharger les parametres.", () => loadData("Parametres actualises.")]
    ]
  }[currentModule] ?? [];

  $("actions").innerHTML = "";
  actions.forEach(([title, detail, handler]) => {
    const card = document.createElement("article");
    card.className = "action-card";
    card.innerHTML = `<span class="action-icon">${actionIcon(title)}</span><div><b>${title}</b><span>${detail}</span></div><button class="primary">${title}</button>`;
    card.querySelector("button").addEventListener("click", handler);
    $("actions").appendChild(card);
  });
}

function actionIcon(title) {
  const value = title.toLowerCase();
  if (value.includes("vente") || value.includes("ticket")) return iconSvg("sales");
  if (value.includes("caisse") || value.includes("cloturer") || value.includes("ouvrir")) return iconSvg("cash");
  if (value.includes("stock")) return iconSvg("stock");
  if (value.includes("produit") || value.includes("categorie")) return iconSvg("products");
  if (value.includes("export") || value.includes("rapport")) return iconSvg("reports");
  if (value.includes("utilisateur") || value.includes("admin") || value.includes("audit")) return iconSvg("users");
  if (value.includes("profil") || value.includes("parametre")) return iconSvg("settings");
  return iconSvg("dashboard");
}

function renderTable() {
  const renderers = {
    dashboard: () => renderRows("Resume", [
      ["Ventes du jour", money(state.dashboard?.todayRevenue), "Chiffre d'affaires valide"],
      ["Tickets", state.dashboard?.todayTicketCount ?? 0, "Tickets valides aujourd'hui"],
      ["Depenses du jour", money(state.dashboard?.todayExpenseTotal), "Charges enregistrees aujourd'hui"],
      ["Solde de caisse", money(state.dashboard?.openCashBalance), "Solde attendu des sessions ouvertes"],
      ["Benefice estimatif", money(state.dashboard?.estimatedProfit), "Marge estimee moins depenses du jour"],
      ["Produits actifs", state.dashboard?.productCount ?? 0, "Catalogue actif"],
      ["Utilisateurs actifs", state.dashboard?.userCount ?? 0, "Comptes disponibles"]
    ]),
    cash: () => renderCashSessions(),
    sales: () => renderSales(),
    products: () => productView === "categories" ? renderCategories("category", state.categories, "Categories produits") : renderProducts(),
    stock: () => renderStock(),
    expenses: () => expenseView === "categories" ? renderCategories("expenseCategory", state.expenseCategories, "Categories depenses") : renderExpenses(),
    reports: () => renderRows("Rapport", [
      ["Ventes du jour", money(state.dashboard?.todayRevenue), "Total des ventes validees"],
      ["Tickets", state.dashboard?.todayTicketCount ?? 0, "Nombre de tickets du jour"],
      ["Depenses", money(state.dashboard?.todayExpenseTotal), "Depenses du jour"],
      ["Benefice estimatif", money(state.dashboard?.estimatedProfit), "Ventes moins couts et depenses du jour"],
      ["Top produits", (state.dashboard?.topProducts ?? []).length, "Classement disponible sur le tableau de bord"],
      ["Sessions ouvertes", state.dashboard?.openCashSessionCount ?? 0, "Sessions caisse en cours"],
      ["Alertes stock", state.dashboard?.lowStockCount ?? 0, "Produits sous seuil"]
    ]),
    users: () => renderUsers(),
    settings: () => renderSettings()
  };

  renderers[currentModule]();
}

function renderCashSessions() {
  const rows = visibleRows(state.cashSessions);
  const headers = [selectHeader(), "Caissier", "Ouverture", "Cloture", "Fond", "Solde attendu", "Declare", "Ecart", "Statut", "Affichage", "Actions"];
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${rows.map((row) => `
    <tr class="${row.isHidden ? "hidden-row" : ""}">
      ${selectCell("cashSession", row.id)}
      <td>${escapeHtml(row.cashierName)}</td>
      <td>${date(row.openedAt)}</td>
      <td>${date(row.closedAt)}</td>
      <td>${money(row.openingAmount)}</td>
      <td>${money(row.expectedClosingAmount)}</td>
      <td>${row.declaredClosingAmount == null ? "" : money(row.declaredClosingAmount)}</td>
      <td>${row.differenceAmount == null ? "" : money(row.differenceAmount)}</td>
      <td>${statusBadge(row.status, row.status === "Ouverte" ? "success" : "neutral")}</td>
      ${hiddenCell(row)}
      <td class="actions">${row.status === "Ouverte" ? `<button data-close-session="${row.id}">Cloturer</button>` : ""}${objectActionButtons("cashSession", row.id, row.isHidden)}</td>
    </tr>
  `).join("")}</tbody>`;

  document.querySelectorAll("[data-close-session]").forEach((button) => {
    button.addEventListener("click", () => openCashCloseModal(Number(button.dataset.closeSession)));
  });
  bindObjectActionButtons();
}

function renderProducts() {
  const rows = visibleRows(state.products);
  const headers = [selectHeader(), "Image", "Produit", "Categorie", "Prix", "Cout", "Stock", "Seuil", "Statut", "Affichage", "Actions"];
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${rows.map((row) => `
    <tr class="${row.isHidden ? "hidden-row" : ""}">
      ${selectCell("product", row.id)}
      <td>${productThumbHtml(row)}</td>
      <td>${escapeHtml(row.name)}</td>
      <td>${escapeHtml(row.categoryName)}</td>
      <td>${money(row.salePrice)}</td>
      <td>${money(row.costPrice)}</td>
      <td>${row.quantity}</td>
      <td>${row.lowStockThreshold}</td>
      <td>${statusBadge(row.isActive ? "Actif" : "Inactif", row.isActive ? "success" : "danger")}</td>
      ${hiddenCell(row)}
      <td class="actions">
        <button data-edit="${row.id}">Modifier</button>
        <button data-toggle="${row.id}" data-active="${!row.isActive}">${row.isActive ? "Desactiver" : "Activer"}</button>
        ${objectActionButtons("product", row.id, row.isHidden)}
      </td>
    </tr>
  `).join("")}</tbody>`;

  document.querySelectorAll("[data-edit]").forEach((button) => {
    button.addEventListener("click", () => openProductModal(Number(button.dataset.edit)));
  });
  document.querySelectorAll("[data-toggle]").forEach((button) => {
    button.addEventListener("click", () => toggleProduct(Number(button.dataset.toggle), button.dataset.active === "true"));
  });
  bindObjectActionButtons();
}

function renderCategories(type, rowsSource, title) {
  const rows = visibleRows(rowsSource);
  const headers = [selectHeader(), "Image", title, "Statut", "Affichage", "Actions"];
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${rows.map((row) => `
    <tr class="${row.isHidden ? "hidden-row" : ""}">
      ${selectCell(type, row.id)}
      <td>${productThumbHtml(row)}</td>
      <td>${escapeHtml(row.name)}</td>
      <td>${statusBadge(row.isActive ? "Active" : "Inactive", row.isActive ? "success" : "danger")}</td>
      ${hiddenCell(row)}
      <td class="actions">${objectActionButtons(type, row.id, row.isHidden)}</td>
    </tr>
  `).join("")}</tbody>`;
  bindObjectActionButtons();
}

function renderSales() {
  const rows = visibleRows(state.sales);
  const headers = [selectHeader(), "Ticket", "Client", "Caissier", "Total", "Paiement", "Statut", "Date", "Affichage", "Actions"];
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${rows.map((row) => `
    <tr class="${row.isHidden ? "hidden-row" : ""}">
      ${selectCell("sale", row.id)}
      <td>${escapeHtml(row.ticketNumber)}</td>
      <td>${escapeHtml(row.customerName ?? "")}</td>
      <td>${escapeHtml(row.cashierName)}</td>
      <td>${money(row.totalAmount)}</td>
      <td>${escapeHtml(row.paymentMethod)}</td>
      <td>${statusBadge(row.status, row.status.toLowerCase().includes("annul") ? "danger" : "success")}</td>
      <td>${date(row.saleDate)}</td>
      ${hiddenCell(row)}
      <td class="actions"><button data-ticket="${row.id}">Ticket</button>${objectActionButtons("sale", row.id, row.isHidden)}</td>
    </tr>
  `).join("")}</tbody>`;
  document.querySelectorAll("[data-ticket]").forEach((button) => {
    button.addEventListener("click", () => openTicketModal(Number(button.dataset.ticket)));
  });
  bindObjectActionButtons();
}

function renderStock() {
  const rows = visibleRows(state.stock);
  const headers = [selectHeader(), "Produit", "Categorie", "Quantite", "Seuil", "Etat", "Affichage", "Actions"];
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${rows.map((row) => `
    <tr class="${row.isHidden ? "hidden-row" : ""}">
      ${selectCell("product", row.productId)}
      <td>${escapeHtml(row.productName)}</td>
      <td>${escapeHtml(row.categoryName)}</td>
      <td>${row.quantity}</td>
      <td>${row.lowStockThreshold}</td>
      <td>${statusBadge(row.isLowStock ? "Stock faible" : "OK", row.isLowStock ? "danger" : "success")}</td>
      ${hiddenCell(row)}
      <td class="actions">${objectActionButtons("product", row.productId, row.isHidden)}</td>
    </tr>
  `).join("")}</tbody>`;
  bindObjectActionButtons();
}

function renderExpenses() {
  const rows = visibleRows(state.expenses);
  const headers = [selectHeader(), "Categorie", "Description", "Montant", "Caisse", "Date", "Statut", "Affichage", "Actions"];
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${rows.map((row) => `
    <tr class="${row.isHidden ? "hidden-row" : ""}">
      ${selectCell("expense", row.id)}
      <td>${escapeHtml(row.categoryName)}</td>
      <td>${escapeHtml(row.description)}</td>
      <td>${money(row.amount)}</td>
      <td>${row.paidFromCashRegister ? "Oui" : "Non"}</td>
      <td>${date(row.expenseDate)}</td>
      <td>${statusBadge(row.status, row.status.toLowerCase().includes("annul") ? "danger" : "success")}</td>
      ${hiddenCell(row)}
      <td class="actions">${objectActionButtons("expense", row.id, row.isHidden)}</td>
    </tr>
  `).join("")}</tbody>`;
  bindObjectActionButtons();
}

function renderUsers() {
  const rows = visibleRows(state.users);
  const headers = [selectHeader(), "Identifiant", "Nom", "Role", "Statut", "2FA", "Creation", "Affichage", "Actions"];
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${rows.map((row) => `
    <tr class="${row.isHidden ? "hidden-row" : ""}">
      ${selectCell("user", row.id)}
      <td>${escapeHtml(row.username)}</td>
      <td>${escapeHtml(row.fullName)}</td>
      <td>${escapeHtml(row.roleName)}</td>
      <td>${statusBadge(row.isActive ? "Actif" : "Inactif", row.isActive ? "success" : "danger")}</td>
      <td>${statusBadge(row.twoFactorEnabled ? "Active" : "Non", row.twoFactorEnabled ? "success" : "neutral")}</td>
      <td>${date(row.createdAt)}</td>
      ${hiddenCell(row)}
      <td class="actions">
        <button data-edit-user="${row.id}">Modifier</button>
        <button data-setup-2fa="${row.id}">${row.twoFactorEnabled ? "Reset 2FA" : "Activer 2FA"}</button>
        <button data-toggle-user="${row.id}" data-active="${!row.isActive}">${row.isActive ? "Desactiver" : "Activer"}</button>
        ${objectActionButtons("user", row.id, row.isHidden)}
      </td>
    </tr>
  `).join("")}</tbody>`;

  document.querySelectorAll("[data-edit-user]").forEach((button) => {
    button.addEventListener("click", () => openUserModal(Number(button.dataset.editUser)));
  });
  document.querySelectorAll("[data-toggle-user]").forEach((button) => {
    button.addEventListener("click", () => toggleUser(Number(button.dataset.toggleUser), button.dataset.active === "true"));
  });
  document.querySelectorAll("[data-setup-2fa]").forEach((button) => {
    button.addEventListener("click", () => openTwoFactorSetup(Number(button.getAttribute("data-setup-2fa"))));
  });
  bindObjectActionButtons();
}

function renderSettings() {
  const profile = state.businessProfile ?? {};
  $("dataTable").innerHTML = tableHead(["Element", "Valeur"]) + `<tbody>
    <tr><td>Nom</td><td>${escapeHtml(profile.name ?? "")}</td></tr>
    <tr><td>Sigle</td><td>${escapeHtml(profile.sigle ?? "")}</td></tr>
    <tr><td>Adresse</td><td>${escapeHtml(profile.address ?? "")}</td></tr>
    <tr><td>Contact</td><td>${escapeHtml(profile.contact ?? "")}</td></tr>
    <tr><td>Logo</td><td>${profile.logoData ? `<img class="thumb" src="${profile.logoData}" alt="">` : ""}</td></tr>
    <tr><td>Image</td><td>${profile.coverImageData ? `<img class="thumb" src="${profile.coverImageData}" alt="">` : ""}</td></tr>
    <tr><td>Pied ticket</td><td>${escapeHtml(profile.ticketFooter ?? "")}</td></tr>
  </tbody>`;
}

function renderObjectTable(headers, rows, projector) {
  $("dataTable").innerHTML = tableHead(headers) + `<tbody>${(rows ?? []).map((row) => {
    return `<tr>${projector(row).map((cell) => `<td>${escapeHtml(String(cell ?? ""))}</td>`).join("")}</tr>`;
  }).join("")}</tbody>`;
}

function renderRows(section, rows) {
  $("dataTable").innerHTML = tableHead(["Section", "Element", "Valeur", "Details"]) +
    `<tbody>${rows.map((row) => `<tr><td>${section}</td><td>${row[0]}</td><td>${row[1]}</td><td>${row[2]}</td></tr>`).join("")}</tbody>`;
}

function renderLowStock() {
  const rows = visibleRows(state.stock).filter((row) => row.isLowStock);
  $("lowStockTable").innerHTML = tableHead(["Produit", "Categorie", "Qte", "Seuil"]) +
    `<tbody>${rows.map((row) => `
      <tr><td>${escapeHtml(row.productName)}</td><td>${escapeHtml(row.categoryName)}</td><td class="badge-low">${row.quantity}</td><td>${row.lowStockThreshold}</td></tr>
    `).join("")}</tbody>`;
}

function tableHead(headers) {
  return `<thead><tr>${headers.map((header) => `<th>${header}</th>`).join("")}</tr></thead>`;
}

function bindForms() {
  $("cashOpenForm").addEventListener("submit", saveCashOpen);
  $("cashCloseForm").addEventListener("submit", saveCashClose);
  $("cashCloseSession").addEventListener("change", () => {
    const selected = (state.cashSessions ?? []).find((session) => session.id === Number($("cashCloseSession").value));
    $("cashCloseAmount").value = selected ? selected.expectedClosingAmount : 0;
  });
  $("saleForm").addEventListener("submit", saveSale);
  $("productForm").addEventListener("submit", saveProduct);
  $("categoryForm").addEventListener("submit", saveCategory);
  $("stockForm").addEventListener("submit", saveStockAdjustment);
  $("expenseForm").addEventListener("submit", saveExpense);
  $("userForm").addEventListener("submit", saveUser);
  $("profileForm").addEventListener("submit", saveProfile);
}

function fillSelect(id, items, selectedId) {
  $(id).innerHTML = items.map((item) =>
    `<option value="${item.id}" ${Number(selectedId) === item.id ? "selected" : ""}>${escapeHtml(item.name)}</option>`
  ).join("");
}

function openModal(id) {
  $(id).showModal();
}

function readImageInput(id) {
  const file = $(id).files?.[0];
  if (!file) {
    return Promise.resolve(null);
  }

  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });
}

function setPreview(id, data) {
  const image = $(id);
  image.src = data || "";
  image.classList.toggle("has-image", Boolean(data));
}

async function openPhotoCamera(target) {
  photoTarget = target;
  const titles = {
    product: "Photo du produit",
    category: "Photo de la categorie",
    profileLogo: "Photo du logo",
    profileCover: "Photo d'accueil"
  };

  $("photoTitle").textContent = titles[target] ?? "Prendre une photo";
  $("photoStatus").textContent = "Demarrage de la camera...";
  openModal("photoModal");

  if (!navigator.mediaDevices?.getUserMedia) {
    $("photoStatus").textContent = "Camera non disponible. Utilise l'import image.";
    return;
  }

  try {
    photoStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" }, audio: false });
    $("photoVideo").srcObject = photoStream;
    await $("photoVideo").play().catch(() => {});
    $("photoStatus").textContent = "Cadre l'image puis clique sur Capturer.";
  } catch {
    $("photoStatus").textContent = "Camera indisponible. Utilise l'import image.";
  }
}

function capturePhoto() {
  const video = $("photoVideo");
  if (!photoStream || video.videoWidth === 0 || video.videoHeight === 0) {
    $("photoStatus").textContent = "Aucune image camera a capturer.";
    return;
  }

  const canvas = $("photoCanvas");
  const maxSide = 900;
  const ratio = Math.min(1, maxSide / Math.max(video.videoWidth, video.videoHeight));
  canvas.width = Math.round(video.videoWidth * ratio);
  canvas.height = Math.round(video.videoHeight * ratio);
  canvas.getContext("2d").drawImage(video, 0, 0, canvas.width, canvas.height);
  applyCapturedPhoto(canvas.toDataURL("image/jpeg", 0.82));
  stopPhotoCamera();
  $("photoModal").close();
  setStatus("Photo capturee.");
}

function applyCapturedPhoto(data) {
  if (photoTarget === "product") {
    productImageData = data;
    setPreview("productImagePreview", data);
  } else if (photoTarget === "category") {
    categoryImageData = data;
    setPreview("categoryImagePreview", data);
  } else if (photoTarget === "profileLogo") {
    profileLogoData = data;
    setPreview("profileLogoPreview", data);
  } else if (photoTarget === "profileCover") {
    profileCoverData = data;
    setPreview("profileCoverPreview", data);
  }
}

function stopPhotoCamera() {
  if (photoStream) {
    photoStream.getTracks().forEach((track) => track.stop());
    photoStream = null;
  }
  $("photoVideo").srcObject = null;
  photoTarget = null;
}

function switchProductView() {
  productView = productView === "products" ? "categories" : "products";
  renderModule();
  setStatus(productView === "products" ? "Produits affiches." : "Categories produits affichees.");
}

function switchExpenseView() {
  expenseView = expenseView === "expenses" ? "categories" : "expenses";
  renderModule();
  setStatus(expenseView === "expenses" ? "Depenses affichees." : "Categories depenses affichees.");
}

function openCashOpenModal() {
  const cashiers = (state.users ?? []).filter((user) => user.isActive && !user.isHidden);
  if (cashiers.length === 0) {
    setStatus("Aucun caissier actif disponible.", true);
    return;
  }

  fillSelect("cashOpenCashier", cashiers, cashiers[0]?.id);
  $("cashOpenAmount").value = 100;
  openModal("cashOpenModal");
}

function openCashCloseModal(id) {
  const sessions = (state.cashSessions ?? [])
    .filter((session) => session.status === "Ouverte" && (!session.isHidden || session.id === id))
    .map((session) => ({
      id: session.id,
      name: `${session.cashierName} - ${date(session.openedAt)} - attendu ${money(session.expectedClosingAmount)}`
    }));
  if (sessions.length === 0) {
    setStatus("Aucune session de caisse ouverte.", true);
    return;
  }

  fillSelect("cashCloseSession", sessions, id ?? sessions[0]?.id);
  const selected = (state.cashSessions ?? []).find((session) => session.id === Number(id ?? sessions[0]?.id));
  $("cashCloseAmount").value = selected ? selected.expectedClosingAmount : 0;
  openModal("cashCloseModal");
}

function openSaleModal() {
  const openSessions = (state.cashSessions ?? [])
    .filter((session) => session.status === "Ouverte" && !session.isHidden)
    .map((session) => ({
      id: session.id,
      name: `${session.cashierName} - ${date(session.openedAt)}`
    }));
  const sellableProducts = saleProducts();
  if (openSessions.length === 0) {
    setStatus("Ouvre d'abord une session de caisse.", true);
    return;
  }

  if (sellableProducts.length === 0) {
    setStatus("Aucun produit vendable disponible.", true);
    return;
  }

  fillSelect("saleCashSession", openSessions, openSessions[0]?.id);
  saleCart = [];
  $("saleCustomerName").value = "";
  fillSelect("saleProduct", sellableProducts.map((product) => ({
    id: product.id,
    name: `${product.name} - ${money(product.salePrice)}${product.isStockManaged ? ` - stock ${product.quantity}` : ""}`
  })), sellableProducts[0]?.id);
  fillSelect("saleCategoryFilter", [
    { id: "all", name: "Toutes les categories" },
    ...(state.categories ?? [])
      .filter((category) => !category.isHidden && sellableProducts.some((product) => product.categoryId === category.id))
      .map((category) => ({ id: String(category.id), name: category.name }))
  ], "all");
  $("saleProductSearch").value = "";
  $("saleQuantity").value = 1;
  $("saleDiscount").value = 0;
  $("salePayment").value = "1";
  renderSaleProductGrid();
  renderSaleCart();
  updateSaleCalculator();
  openModal("saleModal");
}

function saleProducts() {
  return (state.products ?? [])
    .filter((product) => product.isActive && !product.isHidden && (!product.isStockManaged || product.quantity > 0))
    .sort((left, right) => left.name.localeCompare(right.name, "fr"));
}

function renderSaleProductGrid() {
  const search = ($("saleProductSearch").value ?? "").trim().toLowerCase();
  const category = $("saleCategoryFilter").value;
  const rows = saleProducts().filter((product) => {
    const matchesCategory = !category || category === "all" || String(product.categoryId) === category;
    const text = `${product.name} ${product.sku ?? ""} ${product.barcode ?? ""}`.toLowerCase();
    return matchesCategory && (!search || text.includes(search));
  });

  $("saleProductGrid").innerHTML = rows.map((product) => `
    <button type="button" class="sale-product-card" data-sale-product="${product.id}">
      ${productThumbHtml(product, "sale-product-image")}
      <span>${escapeHtml(product.name)}</span>
      <b>${money(product.salePrice)}</b>
      <small>${product.isStockManaged ? `Stock ${product.quantity}` : "Service"} - cliquer pour ajouter</small>
    </button>
  `).join("") || `<p class="empty-state">Aucun produit trouve.</p>`;

  document.querySelectorAll("[data-sale-product]").forEach((button) => {
    button.addEventListener("click", () => addProductToCart(Number(button.getAttribute("data-sale-product"))));
  });
  highlightSaleProduct();
}

function selectSaleProduct(productId) {
  $("saleProduct").value = String(productId);
  $("saleQuantity").value = 1;
  updateSaleCalculator();
  highlightSaleProduct();
}

function addSelectedSaleProductToCart() {
  addProductToCart(Number($("saleProduct").value));
}

function addProductToCart(productId) {
  const product = (state.products ?? []).find((item) => item.id === productId);
  const quantity = Math.max(0, Number($("saleQuantity").value || 0));
  if (!product || product.isHidden || !product.isActive || quantity <= 0) {
    setStatus("Selectionne un produit vendable et une quantite valide.", true);
    return;
  }

  $("saleProduct").value = String(product.id);
  const existing = saleCart.find((item) => item.productId === product.id);
  const nextQuantity = (existing?.quantity ?? 0) + quantity;
  if (product.isStockManaged && nextQuantity > product.quantity) {
    setStatus(`Stock insuffisant pour ${product.name}.`, true);
    return;
  }

  if (existing) {
    existing.quantity = nextQuantity;
  } else {
    saleCart.push({ productId: product.id, quantity });
  }

  $("saleQuantity").value = 1;
  renderSaleCart();
  updateSaleCalculator();
  highlightSaleProduct();
  setStatus(`${product.name} ajoute au panier.`);
}

function clearSaleCart() {
  saleCart = [];
  renderSaleCart();
  updateSaleCalculator();
}

function renderSaleCart() {
  if (saleCart.length === 0) {
    $("saleCartList").innerHTML = `<p class="empty-state">Panier vide.</p>`;
    return;
  }

  $("saleCartList").innerHTML = saleCart.map((line) => {
    const product = (state.products ?? []).find((item) => item.id === line.productId);
    if (!product) {
      return "";
    }

    return `
      <div class="sale-cart-line" data-cart-product="${product.id}">
        ${productThumbHtml(product, "cart-thumb")}
        <div>
          <b>${escapeHtml(product.name)}</b>
          <span>${money(product.salePrice * line.quantity)}</span>
        </div>
        <input type="number" min="0.01" step="0.01" value="${line.quantity}" data-cart-qty="${product.id}">
        <button type="button" class="cart-remove-btn" data-cart-remove="${product.id}" aria-label="Retirer ${escapeHtml(product.name)} du panier">Retirer</button>
      </div>
    `;
  }).join("");

  document.querySelectorAll("[data-cart-qty]").forEach((input) => {
    input.addEventListener("change", () => updateCartQuantity(Number(input.getAttribute("data-cart-qty")), Number(input.value || 0)));
  });
  document.querySelectorAll("[data-cart-remove]").forEach((button) => {
    button.addEventListener("click", () => removeCartLine(Number(button.getAttribute("data-cart-remove"))));
  });
}

function updateCartQuantity(productId, quantity) {
  const product = (state.products ?? []).find((item) => item.id === productId);
  const line = saleCart.find((item) => item.productId === productId);
  if (!line || !product) {
    return;
  }

  if (quantity <= 0) {
    removeCartLine(productId);
    return;
  }

  if (product.isStockManaged && quantity > product.quantity) {
    line.quantity = product.quantity;
    renderSaleCart();
    setStatus(`Quantite limitee au stock disponible pour ${product.name}.`, true);
  } else {
    line.quantity = quantity;
    renderSaleCart();
  }

  updateSaleCalculator();
}

function removeCartLine(productId) {
  saleCart = saleCart.filter((item) => item.productId !== productId);
  renderSaleCart();
  updateSaleCalculator();
}

function highlightSaleProduct() {
  const selectedId = Number($("saleProduct").value);
  document.querySelectorAll("[data-sale-product]").forEach((button) => {
    button.classList.toggle("selected", Number(button.getAttribute("data-sale-product")) === selectedId);
  });
}

function updateSaleCalculator() {
  const product = (state.products ?? []).find((item) => item.id === Number($("saleProduct").value));
  const quantity = Math.max(0, Number($("saleQuantity").value || 0));
  const discount = Math.max(0, Number($("saleDiscount").value || 0));
  const cartSubtotal = saleCart.reduce((sum, line) => {
    const cartProduct = (state.products ?? []).find((item) => item.id === line.productId);
    return sum + (cartProduct ? cartProduct.salePrice * line.quantity : 0);
  }, 0);
  const cartTotal = Math.max(0, cartSubtotal - discount);
  if (!product) {
    $("saleSelectedImage").src = "";
    $("saleSelectedImage").classList.remove("has-image");
    $("saleSelectedName").textContent = "Selectionne un produit";
    $("saleSelectedMeta").textContent = "La calculatrice affichera le prix et le stock.";
    $("saleUnitPrice").textContent = money(0);
    $("saleQtyPreview").textContent = String(quantity || 1);
    $("saleSubtotal").textContent = money(cartSubtotal);
    $("saleDiscountPreview").textContent = money(discount);
    $("saleTotal").textContent = money(cartTotal);
    return;
  }

  $("saleSelectedImage").src = product.imageData || productFallbackImage(product.name);
  $("saleSelectedImage").classList.add("has-image");
  $("saleSelectedName").textContent = product.name;
  $("saleSelectedMeta").textContent = `${product.categoryName} - ${product.isStockManaged ? `stock ${product.quantity}` : "service sans stock"}`;
  $("saleUnitPrice").textContent = money(product.salePrice);
  $("saleQtyPreview").textContent = String(quantity);
  $("saleSubtotal").textContent = money(cartSubtotal);
  $("saleDiscountPreview").textContent = money(discount);
  $("saleTotal").textContent = money(cartTotal);
}

function productThumbHtml(product, className = "thumb") {
  const src = product.imageData || productFallbackImage(product.name);
  return `<img class="${className}" src="${src}" alt="">`;
}

function productFallbackImage(name) {
  const initials = String(name ?? "LP").split(" ").filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase();
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 128 128"><rect width="128" height="128" rx="18" fill="#f3dec3"/><circle cx="64" cy="55" r="28" fill="#7a3f1d"/><text x="64" y="98" text-anchor="middle" font-family="Segoe UI, Arial" font-size="24" font-weight="700" fill="#2b1710">${escapeHtml(initials || "LP")}</text></svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

function openProductModal(id) {
  const product = id ? state.products.find((item) => item.id === id) : null;
  const categories = (state.categories ?? []).filter((category) => !category.isHidden || category.id === product?.categoryId);
  fillSelect("productCategory", categories, product?.categoryId);
  productImageData = product?.imageData ?? null;
  $("productId").value = product?.id ?? "";
  $("productName").value = product?.name ?? "";
  $("productSku").value = product?.sku ?? "";
  $("productBarcode").value = product?.barcode ?? "";
  $("productSalePrice").value = product?.salePrice ?? 0;
  $("productCostPrice").value = product?.costPrice ?? 0;
  $("productThreshold").value = product?.lowStockThreshold ?? 0;
  $("productInitialQty").value = product ? 0 : 0;
  $("productInitialQty").disabled = Boolean(product);
  $("productImageFile").value = "";
  setPreview("productImagePreview", productImageData);
  $("productStockManaged").checked = product?.isStockManaged ?? true;
  $("productActive").checked = product?.isActive ?? true;
  $("productModalTitle").textContent = product ? "Modifier produit" : "Nouveau produit";
  openModal("productModal");
}

function openStockModal() {
  fillSelect("stockProduct", state.products?.filter((p) => p.isStockManaged && !p.isHidden) ?? [], null);
  $("stockDelta").value = 1;
  $("stockReason").value = "Ajustement via formulaire web";
  openModal("stockModal");
}

function openExpenseModal() {
  fillSelect("expenseCategory", (state.expenseCategories ?? []).filter((category) => !category.isHidden), null);
  $("expenseDescription").value = "";
  $("expenseAmount").value = "";
  $("expenseCash").checked = false;
  openModal("expenseModal");
}

function openCategoryModal() {
  categoryImageData = null;
  $("categoryName").value = "";
  $("categoryImageFile").value = "";
  setPreview("categoryImagePreview", null);
  openModal("categoryModal");
}

function openProfileModal() {
  const profile = state.businessProfile ?? {};
  profileLogoData = profile.logoData ?? null;
  profileCoverData = profile.coverImageData ?? null;
  $("profileName").value = profile.name ?? "";
  $("profileSigle").value = profile.sigle ?? "";
  $("profileAddress").value = profile.address ?? "";
  $("profileContact").value = profile.contact ?? "";
  $("profileTicketFooter").value = profile.ticketFooter ?? "";
  $("profileLogoFile").value = "";
  $("profileCoverFile").value = "";
  setPreview("profileLogoPreview", profileLogoData);
  setPreview("profileCoverPreview", profileCoverData);
  openModal("profileModal");
}

function openUserModal(id) {
  const user = id ? state.users.find((item) => item.id === id) : null;
  fillSelect("userRole", state.roles ?? [], user?.roleId);
  $("userId").value = user?.id ?? "";
  $("userUsername").value = user?.username ?? "";
  $("userFullName").value = user?.fullName ?? "";
  $("userPassword").value = "";
  $("userPassword").required = !user;
  $("userActive").checked = user?.isActive ?? true;
  $("userTwoFactor").checked = user?.twoFactorEnabled ?? false;
  $("userResetTwoFactor").checked = false;
  $("userModalTitle").textContent = user ? "Modifier utilisateur" : "Nouvel utilisateur";
  openModal("userModal");
}

async function saveCashOpen(event) {
  event.preventDefault();
  await api("/api/cash/open-session", {
    method: "POST",
    body: {
      cashierId: Number($("cashOpenCashier").value),
      openingAmount: Number($("cashOpenAmount").value)
    }
  });
  $("cashOpenModal").close();
  await loadData("Session de caisse ouverte.");
}

async function saveCashClose(event) {
  event.preventDefault();
  await api("/api/cash/close-session", {
    method: "POST",
    body: {
      cashSessionId: Number($("cashCloseSession").value),
      declaredClosingAmount: Number($("cashCloseAmount").value)
    }
  });
  $("cashCloseModal").close();
  await loadData("Session de caisse cloturee.");
}

async function saveSale(event) {
  event.preventDefault();
  if (saleCart.length === 0) {
    setStatus("Ajoute au moins un produit au panier avant validation.", true);
    return;
  }

  await api("/api/sales/cart", {
    method: "POST",
    body: {
      cashSessionId: Number($("saleCashSession").value),
      customerName: $("saleCustomerName").value || null,
      paymentMethod: Number($("salePayment").value),
      discountAmount: Number($("saleDiscount").value || 0),
      items: saleCart.map((item) => ({ productId: item.productId, quantity: item.quantity }))
    }
  });
  saleCart = [];
  $("saleModal").close();
  await loadData("Vente panier validee.");
}

async function saveProduct(event) {
  event.preventDefault();
  const id = $("productId").value ? Number($("productId").value) : null;
  const body = {
    id,
    categoryId: Number($("productCategory").value),
    name: $("productName").value,
    sku: $("productSku").value || null,
    barcode: $("productBarcode").value || null,
    salePrice: Number($("productSalePrice").value),
    costPrice: Number($("productCostPrice").value),
    isStockManaged: $("productStockManaged").checked,
    lowStockThreshold: Number($("productThreshold").value || 0),
    initialQuantity: Number($("productInitialQty").value || 0),
    isActive: $("productActive").checked,
    imageData: productImageData
  };

  await api(id ? `/api/products/${id}` : "/api/products", { method: id ? "PUT" : "POST", body });
  $("productModal").close();
  await loadData("Produit enregistre.");
}

async function saveCategory(event) {
  event.preventDefault();
  await api("/api/categories", { method: "POST", body: { name: $("categoryName").value, imageData: categoryImageData } });
  $("categoryModal").close();
  await loadData("Categorie creee.");
}

async function saveProfile(event) {
  event.preventDefault();
  await api("/api/profile", {
    method: "POST",
    body: {
      name: $("profileName").value,
      sigle: $("profileSigle").value,
      address: $("profileAddress").value,
      contact: $("profileContact").value,
      logoData: profileLogoData,
      coverImageData: profileCoverData,
      ticketFooter: $("profileTicketFooter").value
    }
  });
  $("profileModal").close();
  await loadData("Profil du bar enregistre.");
}

async function saveStockAdjustment(event) {
  event.preventDefault();
  await api("/api/stock/adjust", {
    method: "POST",
    body: {
      productId: Number($("stockProduct").value),
      quantityDelta: Number($("stockDelta").value),
      reason: $("stockReason").value
    }
  });
  $("stockModal").close();
  await loadData("Stock ajuste.");
}

async function saveExpense(event) {
  event.preventDefault();
  await api("/api/expenses", {
    method: "POST",
    body: {
      categoryId: Number($("expenseCategory").value),
      description: $("expenseDescription").value,
      amount: Number($("expenseAmount").value),
      paidFromCashRegister: $("expenseCash").checked
    }
  });
  $("expenseModal").close();
  await loadData("Depense enregistree.");
}

async function saveUser(event) {
  event.preventDefault();
  const id = $("userId").value ? Number($("userId").value) : null;
  const existing = id ? state.users.find((item) => item.id === id) : null;
  const twoFactorEnabled = $("userTwoFactor").checked;
  const resetTwoFactorSecret = twoFactorEnabled && ($("userResetTwoFactor").checked || !existing?.twoFactorEnabled);
  const saved = await api(id ? `/api/users/${id}` : "/api/users", {
    method: id ? "PUT" : "POST",
    body: {
      id,
      username: $("userUsername").value,
      fullName: $("userFullName").value,
      roleId: Number($("userRole").value),
      password: $("userPassword").value || null,
      isActive: $("userActive").checked,
      twoFactorEnabled,
      resetTwoFactorSecret
    }
  });
  $("userModal").close();
  if (saved && resetTwoFactorSecret) {
    await openTwoFactorSetup(saved.id);
  }

  await loadData("Utilisateur enregistre.");
}

async function toggleProduct(id, isActive) {
  await api(`/api/products/${id}/active`, { method: "POST", body: { isActive } });
  await loadData(isActive ? "Produit active." : "Produit desactive.");
}

async function toggleUser(id, isActive) {
  await api(`/api/users/${id}/active`, { method: "POST", body: { isActive } });
  await loadData(isActive ? "Utilisateur active." : "Utilisateur desactive.");
}

async function openTwoFactorSetup(userId) {
  const setup = await api(`/api/users/${userId}/two-factor/setup`, { method: "POST" });
  if (!setup) {
    return;
  }

  $("twoFactorSetupUser").textContent = `Compte : ${setup.username}`;
  $("twoFactorSecret").value = setup.secret;
  $("twoFactorUri").value = setup.otpAuthUri;
  const user = (state.users ?? []).find((item) => item.id === userId);
  if (user) {
    user.twoFactorEnabled = true;
    renderModule();
  }

  openModal("twoFactorSetupModal");
  setStatus("Cle 2FA generee. Configure-la dans une application Authenticator.");
}

async function copyTwoFactorSecret() {
  const value = $("twoFactorSecret").value;
  if (!value) {
    return;
  }

  try {
    await navigator.clipboard.writeText(value);
    setStatus("Cle 2FA copiee.");
  } catch {
    $("twoFactorSecret").select();
    setStatus("Selectionne la cle 2FA puis copie-la manuellement.");
  }
}

async function setObjectHidden(encodedTarget, isHidden) {
  const [type, id] = encodedTarget.split(":");
  await api(`/api/objects/${type}/${id}/hidden`, { method: "POST", body: { isHidden } });
  await loadData(isHidden ? "Objet masque." : "Objet affiche.");
}

async function deleteObject(encodedTarget) {
  const [type, id] = encodedTarget.split(":");
  if (!window.confirm("Supprimer cet objet de l'affichage ? Les donnees restent archivees dans SQLite.")) {
    return;
  }

  await api(`/api/objects/${type}/${id}/delete`, { method: "POST" });
  await loadData("Objet supprime de l'affichage.");
}

function selectedObjects() {
  return Array.from(selectedTargets).map((target) => {
    const [objectType, id] = target.split(":");
    return { objectType, id: Number(id) };
  });
}

async function bulkSetHidden(isHidden) {
  const objects = selectedObjects();
  if (objects.length === 0) {
    return;
  }

  await api("/api/objects/bulk/hidden", { method: "POST", body: { objects, isHidden } });
  selectedTargets.clear();
  await loadData(isHidden ? "Selection masquee." : "Selection affichee.");
}

async function bulkDelete() {
  const objects = selectedObjects();
  if (objects.length === 0 || !window.confirm(`Supprimer ${objects.length} objet(s) de l'affichage ?`)) {
    return;
  }

  await api("/api/objects/bulk/delete", { method: "POST", body: { objects, isHidden: true } });
  selectedTargets.clear();
  await loadData("Selection supprimee de l'affichage.");
}

async function openTicketModal(saleId = null) {
  const ticket = await api(saleId ? `/api/tickets/${saleId}` : "/api/tickets/last");
  if (!ticket) {
    return;
  }

  renderTicket(ticket);
  openModal("ticketModal");
}

function renderTicket(ticket) {
  const profile = ticket.businessProfile ?? {};
  $("ticketPreview").innerHTML = `
    ${profile.logoData ? `<img src="${profile.logoData}" alt="">` : ""}
    <h3>${escapeHtml(profile.name ?? "")}</h3>
    <p>${escapeHtml(profile.address ?? "")}</p>
    <p>${escapeHtml(profile.contact ?? "")}</p>
    <div class="ticket-line"><span>Ticket</span><b>${escapeHtml(ticket.ticketNumber)}</b></div>
    <div class="ticket-line"><span>Date</span><span>${date(ticket.saleDate)}</span></div>
    <div class="ticket-line"><span>Client</span><span>${escapeHtml(ticket.customerName ?? "Client comptoir")}</span></div>
    <div class="ticket-line"><span>Caissier</span><span>${escapeHtml(ticket.cashierName)}</span></div>
    ${(ticket.items ?? []).map((item) => `
      <div class="ticket-line">
        <span>${escapeHtml(item.productName)} x ${item.quantity}</span>
        <span>${money(item.totalAmount)}</span>
      </div>
    `).join("")}
    <div class="ticket-line"><span>Sous-total</span><span>${money(ticket.subtotalAmount)}</span></div>
    <div class="ticket-line"><span>Remise</span><span>${money(ticket.discountAmount)}</span></div>
    <div class="ticket-line ticket-total"><span>Total</span><span>${money(ticket.totalAmount)}</span></div>
    <p>${escapeHtml(profile.ticketFooter ?? "")}</p>
  `;
}

function downloadExport(type) {
  window.location.href = `/api/exports/${type}`;
}

async function openQrScanner() {
  $("qrManualInput").value = "";
  $("qrStatus").textContent = "Demarrage de la camera...";
  openModal("qrModal");

  if (!("BarcodeDetector" in window) || !navigator.mediaDevices?.getUserMedia) {
    $("qrStatus").textContent = "Scanner automatique non disponible. Saisis le code manuellement.";
    return;
  }

  try {
    qrStream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
    $("qrVideo").srcObject = qrStream;
    const detector = new BarcodeDetector({ formats: ["qr_code", "ean_13", "ean_8", "code_128"] });
    qrTimer = window.setInterval(async () => {
      const codes = await detector.detect($("qrVideo"));
      if (codes.length > 0) {
        $("qrManualInput").value = codes[0].rawValue;
        $("qrStatus").textContent = `Code detecte : ${codes[0].rawValue}`;
        stopQrScanner();
      }
    }, 700);
  } catch {
    $("qrStatus").textContent = "Camera indisponible. Saisis le code manuellement.";
  }
}

function stopQrScanner() {
  if (qrTimer) {
    window.clearInterval(qrTimer);
    qrTimer = null;
  }
  if (qrStream) {
    qrStream.getTracks().forEach((track) => track.stop());
    qrStream = null;
  }
  $("qrVideo").srcObject = null;
}

function useScannedCode() {
  const value = $("qrManualInput").value.trim().toLowerCase();
  if (!value) {
    $("qrStatus").textContent = "Aucun code a utiliser.";
    return;
  }

  const product = (state.products ?? []).find((item) =>
    String(item.barcode ?? "").toLowerCase() === value ||
    String(item.sku ?? "").toLowerCase() === value ||
    item.name.toLowerCase().includes(value));

  if (!product) {
    $("qrStatus").textContent = "Aucun produit correspondant.";
    return;
  }

  stopQrScanner();
  $("qrModal").close();
  currentModule = "products";
  productView = "products";
  renderModule();
  openProductModal(product.id);
  setStatus(`Produit trouve : ${product.name}`);
}

async function postAction(url) {
  const result = await api(url, { method: "POST" });
  if (!result) {
    return;
  }

  await loadData(result.message ?? "Operation terminee.");
}

async function api(url, options = {}) {
  try {
    return await requestJson(url, options);
  } catch (error) {
    if (error.status === 401) {
      showLogin("Session expiree. Connecte-toi de nouveau.");
      return null;
    }

    setStatus(error.message || "Erreur de traitement.", true);
    throw error;
  }
}

async function requestJson(url, options = {}) {
  const response = await fetch(url, {
    method: options.method ?? "GET",
    credentials: "same-origin",
    headers: options.body ? { "Content-Type": "application/json" } : undefined,
    body: options.body ? JSON.stringify(options.body) : undefined
  });

  if (!response.ok) {
    const text = await response.text();
    const error = new Error(text || response.statusText);
    error.status = response.status;
    throw error;
  }

  return response.json();
}

function setStatus(message, error = false) {
  $("status").textContent = message;
  $("status").classList.toggle("error", error);
}

function setLoginStatus(message, error = false) {
  $("loginStatus").textContent = message;
  $("loginStatus").classList.toggle("error", error);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}
