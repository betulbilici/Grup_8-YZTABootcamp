// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// --- Tema değiştirici (Açık / Koyu / Sistem) ---
// FOUC engelleyici script (_Layout.cshtml <head>) sayfa render'ından önce zaten
// data-bs-theme'i localStorage'a göre uyguluyor; burada sadece dropdown etkileşimi
// ve canlı güncelleme (sayfa yenilenmeden) ele alınıyor.
(function () {
    var STORAGE_KEY = "themePreference";
    var mediaQuery = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

    function resolveTheme(preference) {
        if (preference === "system") {
            return mediaQuery && mediaQuery.matches ? "dark" : "light";
        }
        return preference;
    }

    function applyTheme(preference) {
        document.documentElement.setAttribute("data-bs-theme", resolveTheme(preference));
        updateActiveMenuItem(preference);
    }

    function updateActiveMenuItem(preference) {
        var items = document.querySelectorAll('.theme-switcher [data-theme-value]');
        items.forEach(function (item) {
            var isActive = item.getAttribute("data-theme-value") === preference;
            item.classList.toggle("active", isActive);
            if (isActive) {
                item.setAttribute("aria-current", "true");
            } else {
                item.removeAttribute("aria-current");
            }
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        var preference = localStorage.getItem(STORAGE_KEY) || "system";
        updateActiveMenuItem(preference);

        document.querySelectorAll('.theme-switcher [data-theme-value]').forEach(function (item) {
            item.addEventListener("click", function () {
                var value = item.getAttribute("data-theme-value");
                localStorage.setItem(STORAGE_KEY, value);
                applyTheme(value);
            });
        });

        if (mediaQuery) {
            mediaQuery.addEventListener("change", function () {
                var currentPreference = localStorage.getItem(STORAGE_KEY) || "system";
                if (currentPreference === "system") {
                    applyTheme("system");
                }
            });
        }
    });
})();

// --- Erişilebilirlik paneli (Renk Paleti / Yazı Boyutu / Animasyonlar) ---
// Tema değiştiriciyle aynı desen: FOUC script'i sayfa render'ından önce zaten
// attribute'u uyguluyor, burada sadece dropdown etkileşimi ve aktif-öğe
// vurgusu ele alınıyor. Üçü de aynı mantığı taşıdığı için tek bir fabrika
// fonksiyonuyla kuruluyor.
(function () {
    function setupAttributeSwitcher(config) {
        function apply(value) {
            if (value === config.defaultValue) {
                document.documentElement.removeAttribute(config.attribute);
            } else {
                document.documentElement.setAttribute(config.attribute, value);
            }
            updateActive(value);
        }

        function updateActive(value) {
            document.querySelectorAll("[" + config.dataAttr + "]").forEach(function (item) {
                var isActive = item.getAttribute(config.dataAttr) === value;
                item.classList.toggle("active", isActive);
                if (isActive) {
                    item.setAttribute("aria-current", "true");
                } else {
                    item.removeAttribute("aria-current");
                }
            });
        }

        document.addEventListener("DOMContentLoaded", function () {
            var current = localStorage.getItem(config.storageKey) || config.defaultValue;
            updateActive(current);

            document.querySelectorAll("[" + config.dataAttr + "]").forEach(function (item) {
                item.addEventListener("click", function () {
                    var value = item.getAttribute(config.dataAttr);
                    localStorage.setItem(config.storageKey, value);
                    apply(value);
                });
            });
        });
    }

    setupAttributeSwitcher({
        storageKey: "appPalette",
        attribute: "data-app-palette",
        dataAttr: "data-palette-value",
        defaultValue: "default"
    });

    setupAttributeSwitcher({
        storageKey: "appFontSize",
        attribute: "data-app-fontsize",
        dataAttr: "data-fontsize-value",
        defaultValue: "normal"
    });

    setupAttributeSwitcher({
        storageKey: "appMotion",
        attribute: "data-app-motion",
        dataAttr: "data-motion-value",
        defaultValue: "on"
    });
})();
