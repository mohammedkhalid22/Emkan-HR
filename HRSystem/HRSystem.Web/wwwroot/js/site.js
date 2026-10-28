// Fill the shared delete dialog from the clicked button
document.addEventListener('click', function (e) {
    const button = e.target.closest('[data-delete-url]');
    if (!button) return;

    document.getElementById('deleteForm').action = button.dataset.deleteUrl;
    document.getElementById('deleteName').textContent = button.dataset.deleteName;
    bootstrap.Modal.getOrCreateInstance(document.getElementById('deleteModal')).show();
});
