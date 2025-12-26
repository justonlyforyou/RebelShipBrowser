// ==UserScript==
// @name        Shipping Manager - Reputation Display
// @description Shows reputation next to company name, click to open Finance modal
// @version     3.6
// @author      joseywales - Pimped by https://github.com/justonlyforyou/
// @match       https://shippingmanager.cc/*
// @grant       none
// @run-at      document-end
// @enabled     false
// ==/UserScript==

(function() {
    'use strict';

    const API_URL = "https://shippingmanager.cc/api/user/get-user-settings";
    let reputationElement = null;

    function getReputationColor(rep) {
        if (rep >= 80) return "#8fffa1";
        if (rep >= 50) return "#fff176";
        return "#ff8a80";
    }

    function createReputationLink() {
        if (reputationElement) return reputationElement;

        const companyContent = document.querySelector('.companyContent');
        if (!companyContent) return null;

        // Create reputation element
        reputationElement = document.createElement('div');
        reputationElement.id = 'reputation-display';
        reputationElement.style.cssText = `
            display: inline-flex;
            align-items: center;
            margin-left: 10px;
            padding: 2px 8px;
            border-radius: 4px;
            font-size: 13px;
            font-weight: bold;
            cursor: pointer;
            background: #ffdf5c;
            color: #333;
        `;
        reputationElement.textContent = 'Reputation: ...';

        // Click handler - open Finance modal via stockInfo click
        reputationElement.addEventListener('click', () => {
            const stockInfo = document.querySelector('.stockInfo');
            if (stockInfo) {
                stockInfo.click();
                // Wait for modal to open, then click Marketing tab
                setTimeout(() => {
                    const marketingBtn = document.getElementById('marketing-page-btn');
                    if (marketingBtn) {
                        marketingBtn.click();
                    }
                }, 300);
            }
        });

        // Insert after stockInfo
        const stockInfo = companyContent.querySelector('.stockInfo');
        if (stockInfo && stockInfo.parentNode) {
            stockInfo.parentNode.insertBefore(reputationElement, stockInfo.nextSibling);
        } else {
            companyContent.appendChild(reputationElement);
        }

        return reputationElement;
    }

    async function updateReputation() {
        try {
            const response = await fetch(API_URL, { credentials: "include" });
            if (!response.ok) return;

            const data = await response.json();
            const rep = data?.user?.reputation;

            if (rep === undefined || rep === null) return;

            // Try to create/find the element
            let el = document.getElementById('reputation-display');
            if (!el) {
                el = createReputationLink();
            }

            if (el) {
                el.textContent = `Reputation: ${rep}%`;
                el.style.background = getReputationColor(rep);
                el.style.color = rep >= 80 ? '#333' : '#330000';
            }
        } catch (err) {
            console.error("[Reputation] Error:", err);
        }
    }

    // Wait for page to load, then start
    function init() {
        const companyContent = document.querySelector('.companyContent');
        if (companyContent) {
            updateReputation();
            setInterval(updateReputation, 2 * 60 * 1000);
        } else {
            setTimeout(init, 1000);
        }
    }

    init();
})();
