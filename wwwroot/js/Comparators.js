// comparators.js
window.VersionRangeFilter = class {
    init(params) {
        this.params = params;
        this.filterChangedCallback = params.filterChangedCallback;

        this.minInput = document.createElement('input');
        this.minInput.type = 'text';
        this.minInput.placeholder = 'Min version';
        this.minInput.style.width = '80px';
        this.minInput.addEventListener('input', () => this.filterChangedCallback());

        this.maxInput = document.createElement('input');
        this.maxInput.type = 'text';
        this.maxInput.placeholder = 'Max version';
        this.maxInput.style.width = '80px';
        this.maxInput.addEventListener('input', () => this.filterChangedCallback());

        this.gui = document.createElement('div');
        this.gui.style.display = 'flex';
        this.gui.style.gap = '4px';
        this.gui.appendChild(this.minInput);
        this.gui.appendChild(this.maxInput);
    }

    getGui() {
        return this.gui;
    }

    versionToNumber(v) {
        if (!v) return 0;
        const parts = v.split('.').map(n => parseInt(n, 10));
        return (parts[0] || 0) * 1e6 + (parts[1] || 0) * 1e4 + (parts[2] || 0) * 1e2 + (parts[3] || 0);
    }

    doesFilterPass(params) {
        const value = params.data[this.params.colDef.field];
        const valueNum = this.versionToNumber(value);
        const minNum = this.versionToNumber(this.minInput.value);
        const maxNum = this.versionToNumber(this.maxInput.value);

        if (this.minInput.value && valueNum < minNum) return false;
        if (this.maxInput.value && valueNum > maxNum) return false;
        return true;
    }

    isFilterActive() {
        return !!this.minInput.value || !!this.maxInput.value;
    }

    getModel() {
        return {
            min: this.minInput.value,
            max: this.maxInput.value
        };
    }

    setModel(model) {
        this.minInput.value = model ? model.min : '';
        this.maxInput.value = model ? model.max : '';
    }
};
