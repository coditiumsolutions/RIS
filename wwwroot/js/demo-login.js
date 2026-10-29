(() => {
  const button = document.getElementById("fill-demo-credentials");
  if (!button) {
    return;
  }

  button.addEventListener("click", () => {
    const username = button.getAttribute("data-demo-username") ?? "";
    const password = button.getAttribute("data-demo-password") ?? "";
    const userInput = document.getElementById("Username");
    const passInput = document.getElementById("Password");

    if (userInput) {
      userInput.value = username;
      userInput.dispatchEvent(new Event("input", { bubbles: true }));
      userInput.dispatchEvent(new Event("change", { bubbles: true }));
    }

    if (passInput) {
      passInput.value = password;
      passInput.dispatchEvent(new Event("input", { bubbles: true }));
      passInput.dispatchEvent(new Event("change", { bubbles: true }));
    }

    userInput?.focus();
  });
})();
