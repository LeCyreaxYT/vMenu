"use strict";

(() => {
    const menu = document.getElementById("menuapi");

    window.addEventListener("message", event => {
        const data = event.data;

        if (data?.type === "menu_appearance" && Number.isFinite(data.scale)) {
            menu.style.setProperty("--vmenu-menu-scale", data.scale);
        }
    });
})();
