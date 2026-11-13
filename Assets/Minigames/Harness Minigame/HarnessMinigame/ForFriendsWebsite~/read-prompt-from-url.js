// Paste this into the AI website builder (friend's site) so the game can prefill the prompt box.
// The game opens: https://your-site.com/?prompt=<the player's template>
// Change "#prompt" to the id of your prompt <textarea>.
document.addEventListener("DOMContentLoaded", () => {
  const prompt = new URLSearchParams(window.location.search).get("prompt");
  const box = document.querySelector("#prompt");
  if (prompt && box) {
    box.value = prompt;
    box.focus();
    // Put the cursor on the first [blank] so the player can type straight away
    const start = prompt.indexOf("[");
    const end = prompt.indexOf("]", start) + 1;
    if (start >= 0 && end > start) box.setSelectionRange(start, end);
  }
});
