// ApexPost: progressive enhancement only (CSP allows same-origin scripts, no inline handlers).
// Buttons with data-confirm ask before submitting (voids, refunds, deactivation).
document.addEventListener("submit", function (event) {
    var button = event.submitter;
    var message = button && button.getAttribute("data-confirm");
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});
