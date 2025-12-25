// ==UserScript==
// @name         ShippingManager - Bunker Price Display
// @namespace    http://tampermonkey.net/
// @version      1.2
// @description  Shows current fuel and CO2 bunker prices with color coding
// @author       https://github.com/justonlyforyou/
// @match        https://shippingmanager.cc/*
// @grant        none
// @run-at       document-end
// @enabled      false
// ==/UserScript==

(function() {
    'use strict';

    const API_URL = "https://shippingmanager.cc/api/bunker/get-prices";
    let fuelPriceElement = null;
    let co2PriceElement = null;

    /**
     * Find the current price entry based on UTC time
     * API delivers 12h of prices in UTC
     */
    function findCurrentPrice(prices) {
        const now = new Date();
        const utcHours = now.getUTCHours();
        const utcMinutes = now.getUTCMinutes();

        // Build current UTC time slot string (30 min intervals)
        const currentSlot = utcMinutes < 30
            ? `${String(utcHours).padStart(2, '0')}:00`
            : `${String(utcHours).padStart(2, '0')}:30`;

        // Find matching time slot
        const match = prices.find(p => p.time === currentSlot);
        if (match) return match;

        // Fallback: first entry
        return prices[0];
    }

    // Color thresholds matching our forecast
    function getFuelColor(price) {
        if (price > 750) return '#ef4444';  // red
        if (price >= 650) return '#fbbf24'; // orange
        if (price >= 500) return '#60a5fa'; // blue
        return '#4ade80';                   // green
    }

    function getCO2Color(price) {
        if (price >= 20) return '#ef4444';  // red
        if (price >= 15) return '#fbbf24';  // orange
        if (price >= 10) return '#60a5fa';  // blue
        return '#4ade80';                   // green
    }

    function createPriceElement(text) {
        const el = document.createElement('span');
        el.style.cssText = 'margin-left:8px;font-weight:bold;font-size:13px;';
        el.textContent = text;
        return el;
    }

    function insertPriceDisplays() {
        // Find fuel chart element
        const chartElement = document.querySelector('.content.chart.cursor-pointer');
        if (chartElement && !fuelPriceElement) {
            fuelPriceElement = createPriceElement('...');
            fuelPriceElement.id = 'bunker-fuel-price';
            chartElement.parentNode.insertBefore(fuelPriceElement, chartElement.nextSibling);
        }

        // Find CO2 LED element
        const ledElement = document.querySelector('.content.led.cursor-pointer');
        if (ledElement && !co2PriceElement) {
            co2PriceElement = createPriceElement('...');
            co2PriceElement.id = 'bunker-co2-price';
            ledElement.parentNode.insertBefore(co2PriceElement, ledElement.nextSibling);
        }

        return fuelPriceElement && co2PriceElement;
    }

    async function updatePrices() {
        try {
            const response = await fetch(API_URL, { credentials: "include" });
            if (!response.ok) return;

            const data = await response.json();
            const prices = data?.data?.prices;

            if (!prices || prices.length === 0) return;

            // Use discounted prices if available, otherwise find current price
            const discountedFuel = data?.data?.discounted_fuel;
            const discountedCo2 = data?.data?.discounted_co2;

            let fuelPrice, co2Price;
            if (discountedFuel !== undefined) {
                fuelPrice = discountedFuel;
            } else {
                const currentPrice = findCurrentPrice(prices);
                fuelPrice = currentPrice.fuel_price;
            }

            if (discountedCo2 !== undefined) {
                co2Price = discountedCo2;
            } else {
                const currentPrice = findCurrentPrice(prices);
                co2Price = currentPrice.co2_price;
            }

            // Ensure elements exist
            if (!fuelPriceElement || !co2PriceElement) {
                if (!insertPriceDisplays()) return;
            }

            // Update fuel price
            if (fuelPriceElement && fuelPrice !== undefined) {
                fuelPriceElement.textContent = '$' + fuelPrice + '/t';
                fuelPriceElement.style.color = getFuelColor(fuelPrice);
            }

            // Update CO2 price
            if (co2PriceElement && co2Price !== undefined) {
                co2PriceElement.textContent = '$' + co2Price + '/t';
                co2PriceElement.style.color = getCO2Color(co2Price);
            }

        } catch (err) {
            console.error("[BunkerPrice] Error:", err);
        }
    }

    function init() {
        if (insertPriceDisplays()) {
            updatePrices();
            // Update every 30 seconds
            setInterval(updatePrices, 30 * 1000);
        } else {
            // Retry until elements are found
            setTimeout(init, 1000);
        }
    }

    init();
})();
