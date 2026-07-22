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
