(() => {
  const buttons = [
    ...document.querySelectorAll(".js-fill-demo-credentials"),
    ...document.querySelectorAll("#fill-demo-credentials")
  ].filter((el, index, list) => list.indexOf(el) === index);

  if (buttons.length === 0) {
    return;
  }

  const fill = (button) => {
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
  };

  for (const button of buttons) {
    button.addEventListener("click", () => fill(button));
  }
})();
