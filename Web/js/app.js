        // ================================================================
        // MSFS REMOTE BUTTONS - CLIENT WEB
        // ================================================================
        //
        // Architecture:
        // - WebSocket pour la communication temps r├®el avec le serveur
        // - G├®n├®ration dynamique de l'interface selon le profil d'avion
        // - Mise ├á jour visuelle instantan├®e des ├®tats (LEDs, s├®lecteurs)
        //
        // Flux de donn├®es:
        // 1. Connexion WebSocket ÔåÆ R├®ception du profil ÔåÆ G├®n├®ration de l'UI
        // 2. Clic bouton ÔåÆ Envoi commande ÔåÆ Serveur ÔåÆ MSFS
        // 3. MSFS change ├®tat ÔåÆ Serveur ÔåÆ WebSocket ÔåÆ Mise ├á jour UI
        //
        // ================================================================

        /** Avion correspondant \u00e0 la page actuelle (ex: PC12 pour /PC12/) */
        function getCurrentPageAircraftId() {
            const m = window.location.pathname.match(/^\/([A-Za-z0-9]+)\/?/);
            return m ? m[1].toUpperCase() : null;
        }

        // === \u00c9TAT GLOBAL ===
        let ws = null;               // Instance WebSocket
        let profile = null;          // Profil d'avion actuel (commandes, cat├®gories)
        let states = {};             // Cache des ├®tats: { commandId: valeur }
        let reconnectTimeout = null; // Timer pour reconnexion auto
        let currentOAT = null;       // Temp\u00e9rature ext\u00e9rieure actuelle (OAT)
        var pageLoadTime = Date.now(); // Pour retry rapide au rafra\u00eechissement

        // === ├ëL├ëMENTS DOM ===
        const controlsEl = document.getElementById('controls');
        const wsStatusEl = document.getElementById('wsStatus');
        const simStatusEl = document.getElementById('simStatus');
        const aircraftNameEl = document.getElementById('aircraftName');
        const disconnectedOverlay = document.getElementById('disconnectedOverlay');
        const oatDisplayEl = document.getElementById('oatDisplay');
        const trimDisplayEl = document.getElementById('trimDisplay');

        // ================================================================
        // CONNEXION WEBSOCKET
        // ================================================================

        /**
         * ├ëtablit la connexion WebSocket avec le serveur
         * G├¿re automatiquement la reconnexion en cas de perte de connexion
         */
        var connectTimeoutId = null;

        function connect() {
            if (connectTimeoutId) { clearTimeout(connectTimeoutId); connectTimeoutId = null; }
            var protocol = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
            var wsUrl = protocol + '//' + window.location.host + '/ws';

            console.log('Connexion \u00e0', wsUrl);
            ws = new WebSocket(wsUrl);

            connectTimeoutId = setTimeout(function() {
                if (ws && ws.readyState !== WebSocket.OPEN) {
                    ws.close();
                    connectTimeoutId = null;
                }
            }, 6000);

            ws.onopen = function() {
                if (connectTimeoutId) { clearTimeout(connectTimeoutId); connectTimeoutId = null; }
                console.log('WebSocket connect\u00e9');
                wsStatusEl.classList.add('connected');
                disconnectedOverlay.classList.remove('show');
                ws.send(JSON.stringify({ type: 'getState' }));
            };

            ws.onclose = function() {
                if (connectTimeoutId) { clearTimeout(connectTimeoutId); connectTimeoutId = null; }
                console.log('WebSocket d\u00e9connect\u00e9');
                wsStatusEl.classList.remove('connected');
                simStatusEl.classList.remove('connected');
                var justLoaded = (Date.now() - pageLoadTime) < 2500;
                if (!justLoaded) {
                    disconnectedOverlay.classList.add('show');
                    var msgEl = disconnectedOverlay.querySelector('p');
                    if (msgEl) msgEl.innerHTML = 'Connexion au serveur perdue ou impossible.<br>V\u00e9rifiez que l\'application <strong>MSFS Remote Buttons</strong> est lanc\u00e9e sur le PC (<code>dotnet run</code> ou F5), puis ouvrez <strong>' + window.location.origin + '</strong> dans le navigateur.';
                }
                if (reconnectTimeout) clearTimeout(reconnectTimeout);
                var delay = justLoaded ? 400 : 1500;
                reconnectTimeout = setTimeout(connect, delay);
            };

            ws.onerror = function(err) {
                console.error('Erreur WebSocket:', err);
            };

            // R├®ception d'un message du serveur
            ws.onmessage = (event) => {
                try {
                    const msg = JSON.parse(event.data);
                    handleMessage(msg);
                } catch (e) {
                    console.error('Erreur parsing message:', e);
                }
            };
        }

        // ================================================================
        // TRAITEMENT DES MESSAGES SERVEUR
        // ================================================================

        /**
         * Route les messages re├ºus du serveur vers les handlers appropri├®s
         * Types: connection, aircraft, state, pong
         */
        function handleMessage(msg) {
            switch (msg.type) {
                case 'connection':
                    if (msg.data.connected) {
                        simStatusEl.classList.add('connected');
                    } else {
                        simStatusEl.classList.remove('connected');
                        aircraftNameEl.textContent = 'MSFS non connect├®';
                    }
                    break;

                case 'aircraft':
                    aircraftNameEl.textContent = msg.data.title || 'Inconnu';
                    if (msg.data.profile) {
                        profile = msg.data.profile;
                        var pageId = getCurrentPageAircraftId();
                        if (pageId && profile.id && profile.id !== pageId) {
                            window.location.href = '/' + profile.id + '/';
                            return;
                        }
                        renderControls();
                    }
                    break;

                case 'state':
                    states[msg.data.id] = msg.data.value;
                    updateControlState(msg.data.id, msg.data.value);
                    // Mise ├á jour du trim si c'est la commande display_elevator_trim
                    if (msg.data.id === 'display_elevator_trim') {
                        // ELEVATOR TRIM PCT avec "Percent Over 100" retourne une valeur entre -1 et 1
                        // Multiplier par 100 pour obtenir un pourcentage (-100% ├á +100%)
                        // Pour le C172, la plage r├®elle est environ -86.4% ├á +100%
                        const trimValue = parseFloat(msg.data.value || 0);
                        const trimPercent = trimValue * 100;
                        if (trimDisplayEl) { trimDisplayEl.textContent = `TRIM: ${trimPercent >= 0 ? '+' : ''}${trimPercent.toFixed(1)}%`; updateTrimGauge(trimPercent); }
                    }
                    break;

                case 'environmentUpdate':
                    if (msg.data && msg.data.oat !== undefined) {
                        const oat = parseFloat(msg.data.oat);
                        currentOAT = oat;
                        oatDisplayEl.textContent = 'OAT: ' + oat.toFixed(1) + '\u00B0C';
                        updateOATWarningLED();
                    }
                    break;

                case 'fuelUpdate':
                    if (msg.data) {
                        const leftGallons = parseFloat(msg.data.left || 0);
                        const rightGallons = parseFloat(msg.data.right || 0);
                        updateFuelQuantities(leftGallons, rightGallons);
                    }
                    break;

                case 'pong':
                    // Heartbeat OK - Connexion toujours active
                    break;
            }
        }

        // ================================================================
        // G├ëN├ëRATION DE L'INTERFACE
        // ================================================================

        /** Layout g\u00e9n\u00e9rique (PC12, etc.) - une cat\u00e9gorie / boutons par bloc */
        function buildGenericLayout() {
            if (!profile) return '';
            var html = '';
            for (var ci = 0; ci < profile.categories.length; ci++) {
                var category = profile.categories[ci];
                var commands = profile.commands.filter(function(c) { return c.category === category && !c.hidden; });
                if (commands.length === 0) continue;
                html += '<div class="category" data-category="' + category + '">';
                html += '<div class="category-title">' + category + '</div>';
                if (category === 'AUTOPILOT') {
                    html += '<div class="autopilot-panel">';
                    html += '<div class="displays-row">';
                    html += '<div class="display-spacer"></div><div class="display-spacer"></div>';
                    html += '<div class="display-box"><div class="display-label">HDG</div><div class="display-value" id="display-hdg">---</div></div>';
                    html += '<div class="display-box"><div class="display-label">ALT</div><div class="display-value" id="display-alt">-----</div></div>';
                    html += '<div class="display-spacer"></div><div class="display-spacer"></div><div class="display-spacer"></div>';
                    html += '<div class="display-box"><div class="display-label">VS</div><div class="display-value" id="display-vs">----</div></div>';
                    html += '<div class="display-box"><div class="display-label">SPD</div><div class="display-value" id="display-spd">---</div></div>';
                    html += '</div>';
                    html += '<div class="category-buttons">';
                    for (var i = 0; i < commands.length; i++) {
                        if (commands[i].controlType === 'toggle') html += renderToggle(commands[i]);
                    }
                    html += '</div>';
                    html += '<div class="controls-row">';
                    html += '<div class="control-spacer"></div><div class="control-spacer"></div>';
                    html += '<div class="control-group vs-controls"><button class="ctrl-btn" onclick="sendHdgIncrement(true)">+</button><button class="ctrl-btn" onclick="sendHdgIncrement(false)">\u2212</button></div>';
                    html += '<div class="control-group vs-controls"><button class="ctrl-btn" onclick="sendAltIncrement(true)">+</button><button class="ctrl-btn" onclick="sendAltIncrement(false)">\u2212</button></div>';
                    html += '<div class="control-spacer"></div><div class="control-spacer"></div><div class="control-spacer"></div>';
                    html += '<div class="control-group vs-controls"><button class="ctrl-btn" onclick="sendToggle(\'vs_inc\')">+</button><button class="ctrl-btn" onclick="sendToggle(\'vs_dec\')">\u2212</button></div>';
                    html += '<div class="control-group vs-controls"><button class="ctrl-btn" onclick="sendToggle(\'spd_inc\')">+</button><button class="ctrl-btn" onclick="sendToggle(\'spd_dec\')">\u2212</button></div>';
                    html += '</div></div>';
                } else {
                    html += '<div class="category-buttons">';
                    for (var j = 0; j < commands.length; j++) {
                        var c = commands[j];
                        if (c.controlType === 'toggle') html += renderToggle(c);
                        else if (c.controlType === 'selector') html += renderSelector(c);
                        else if (c.controlType === 'momentary') html += renderMomentary(c);
                        else if (c.controlType === 'potentiometer') html += renderPotentiometer(c);
                    }
                    html += '</div>';
                }
                html += '</div>';
            }
            return html;
        }

        /**
         * G\u00e9n\u00e8re l'interface compl\u00e8te selon le profil d'avion
         * Appel\u00e9 quand un nouveau profil est re\u00e7u (changement d'avion)
         */
        function renderControls() {
            if (!profile) {
                controlsEl.innerHTML = '<div class="loading">En attente du profil...</div>';
                return;
            }

            var html = buildGenericLayout();

            controlsEl.innerHTML = html;


            // Attacher les event listeners pour les potentiom├¿tres
            document.querySelectorAll('.potentiometer-slider').forEach(slider => {
                const cmdId = slider.dataset.cmdId;
                let lastSentValue = -1; // Valeur initiale pour forcer l'envoi de la premi├¿re valeur
                let isDragging = false;
                
                const updateAndSend = (value) => {
                    const potentiometer = slider.closest('.potentiometer-control');
                    const valueLabel = potentiometer.querySelector('.potentiometer-value');
                    const fillBar = potentiometer.querySelector('.potentiometer-fill');
                    
                    // Mise ├á jour visuelle imm├®diate
                    if (valueLabel) valueLabel.textContent = `${value}%`;
                    if (fillBar) fillBar.style.width = `${value}%`;
                    
                    // Envoi de commande seulement si la valeur a chang├®
                    if (lastSentValue !== value) {
                        sendPotentiometer(cmdId, value);
                        lastSentValue = value;
                    }
                };
                
                // Event 'input': mise ├á jour visuelle + envoi de commande en temps r├®el
                slider.addEventListener('input', (e) => {
                    const value = parseInt(e.target.value);
                    updateAndSend(value);
                });
                
                // Event 'mousedown': marquer le d├®but du drag
                slider.addEventListener('mousedown', () => {
                    isDragging = true;
                });
                
                // Event 'mousemove': capturer les changements pendant le drag
                slider.addEventListener('mousemove', (e) => {
                    if (isDragging && e.buttons === 1) {
                        const value = parseInt(slider.value);
                        updateAndSend(value);
                    }
                });
                
                // Event 'mouseup': fin du drag
                slider.addEventListener('mouseup', () => {
                    isDragging = false;
                });
                
                // Event 'touchstart': marquer le d├®but du drag tactile
                slider.addEventListener('touchstart', () => {
                    isDragging = true;
                });
                
                // Event 'touchmove': capturer les changements pendant le drag tactile
                slider.addEventListener('touchmove', () => {
                    if (isDragging) {
                        const value = parseInt(slider.value);
                        updateAndSend(value);
                    }
                });
                
                // Event 'touchend': fin du drag tactile
                slider.addEventListener('touchend', () => {
                    isDragging = false;
                });
                
                // Event 'change': envoie la commande finale quand l'utilisateur rel├óche (s├®curit├®)
                slider.addEventListener('change', (e) => {
                    const value = parseInt(e.target.value);
                    // Envoyer seulement si la valeur finale est diff├®rente de la derni├¿re envoy├®e
                    if (lastSentValue !== value) {
                        sendPotentiometer(cmdId, value);
                        lastSentValue = value;
                    }
                });
            });

            // Attacher les event listeners pour les switches cylindriques (zone de touch optimis├®e)
            document.querySelectorAll('.cylindrical-switch').forEach(switchEl => {
                const cmdId = switchEl.dataset.id;
                // Rendre toute la zone switch cliquable pour le tactile
                switchEl.style.cursor = 'pointer';
                switchEl.addEventListener('click', () => {
                    sendToggle(cmdId);
                });
            });

            // Initialiser les s├®lecteurs d'incr├®ment
            setHdgIncrement(1); // Valeur par d├®faut : 1
            setAltIncrement(100); // Valeur par d├®faut : 100

            // Appliquer les ├®tats d├®j├á connus aux nouveaux ├®l├®ments
            for (const [id, value] of Object.entries(states)) {
                updateControlState(id, value);
                // Initialiser l'affichage du trim si disponible
                if (id === 'display_elevator_trim' && trimDisplayEl) {
                    const trimValue = parseFloat(value || 0);
                    const trimPercent = trimValue * 100;
                    trimDisplayEl.textContent = `TRIM: ${trimPercent >= 0 ? '+' : ''}${trimPercent.toFixed(1)}%`;
                    updateTrimGauge(trimPercent);
                }
            }

            // Mettre ├á jour la LED d'avertissement OAT si l'OAT est d├®j├á disponible
            updateOATWarningLED();
        }

        /**
         * G├®n├¿re le HTML pour un bouton Toggle (ON/OFF avec LED)
         */
        function renderToggle(cmd) {
            const state = states[cmd.id] || 0;
            const stateClass = state > 0.5 ? 'on' : 'off';
            
            // LED rouge pour pitot_heat quand OAT < 4.4┬░C (├®teinte par d├®faut)
            const showOATWarning = cmd.id === 'pitot_heat' ? '<div class="oat-warning-led inactive" id="oat-warning-pitot"></div>' : '';
            
            return `
                <div class="toggle-wrapper" data-toggle-id="${cmd.id}">
                    <button class="btn-toggle ${stateClass}" data-id="${cmd.id}" onclick="sendToggle('${cmd.id}')">
                        <div class="indicator"></div>
                        <span class="label">${cmd.name}</span>
                    </button>
                    ${showOATWarning}
                </div>
            `;
        }

        /**
         * Met ├á jour les quantit├®s de carburant affich├®es sous les boutons LEFT et RIGHT
         * @param {number} leftGallons - Quantit├® dans le r├®servoir gauche (gallons)
         * @param {number} rightGallons - Quantit├® dans le r├®servoir droit (gallons)
         */
        function updateFuelQuantities(leftGallons, rightGallons) {
            const leftEl = document.getElementById('fuel-left-quantity');
            const rightEl = document.getElementById('fuel-right-quantity');
            
            if (leftEl) {
                leftEl.textContent = `${leftGallons.toFixed(1)}gal`;
            }
            if (rightEl) {
                rightEl.textContent = `${rightGallons.toFixed(1)}gal`;
            }
        }

        /**
         * Met ├á jour la jauge de trim de profondeur (verticale)
         * @param {number} trimPercent - Position du trim en pourcentage (-86.4% ├á +100%)
         */
        function updateTrimGauge(trimPercent) {
            const indicatorBar = document.getElementById('trim-indicator-bar');
            if (!indicatorBar) return;

            // Plage du trim : -86.4% ├á +100%
            const minTrim = -86.4;
            const maxTrim = 100;
            const range = maxTrim - minTrim; // 186.4

            // Calculer la position en pourcentage (0-100%) de la jauge
            // Normaliser : (trimPercent - minTrim) / range
            const normalizedPosition = ((trimPercent - minTrim) / range) * 100;
            const clampedPosition = Math.max(0, Math.min(100, normalizedPosition));

            // Inverser pour que -86.4% soit en haut et que le curseur monte quand le trim augmente
            const invertedPosition = 100 - clampedPosition;

            // Positionner la barre indicateur verticalement
            // Utiliser bottom invers├® : -86.4% en haut, +100% en bas, curseur monte quand trim augmente
            indicatorBar.style.bottom = `${invertedPosition}%`;
            indicatorBar.style.top = 'auto';
            indicatorBar.style.transform = 'translateX(-50%)'; // Centrer horizontalement
        }

        /**
         * Met ├á jour la LED d'avertissement OAT sous le bouton pitot_heat
         * La LED s'allume en rouge quand OAT < 4.4┬░C
         */
        function updateOATWarningLED() {
            const ledEl = document.getElementById('oat-warning-pitot');
            if (!ledEl) {
                return;
            }
            
            if (currentOAT !== null && currentOAT < 4.4) {
                ledEl.classList.add('active');
                ledEl.classList.remove('inactive');
            } else {
                ledEl.classList.remove('active');
                ledEl.classList.add('inactive');
            }
        }

        /**
         * G├®n├¿re le HTML pour un Selector (multi-positions)
         * Cas sp├®cial pour les volets (flaps) avec boutons +/-
         */
        function renderSelector(cmd) {
            if (!cmd.options) return '';

            const currentValue = states[cmd.id] || 0;

            let optionsHtml = '';
            for (const opt of cmd.options) {
                const active = Math.abs(currentValue - opt.value) < 0.5 ? 'active' : '';
                optionsHtml += `
                    <button class="selector-option ${active}"
                            data-id="${cmd.id}"
                            data-value="${opt.value}"
                            onclick="sendSelector('${cmd.id}', '${opt.simEvent}', ${opt.value})">
                        ${opt.label}
                    </button>
                `;
            }

            // Special layout for flaps: realistic vertical lever selector with +/- buttons
            if (cmd.id === 'flaps') {
                const currentValue = states[cmd.id] || 0;
                // Calculer la position du levier (0 = UP, 1 = 10┬░, 2 = 20┬░, 3 = FULL)
                const leverPosition = Math.round(currentValue);
                // Positions non-lin├®aires : 10┬░ ├á 40%, 20┬░ ├á 60%
                // UP (0): top = 0% + 8px/184px Ôëê 4.3% (un peu moins haut)
                // 10┬░ (1): top = 40% - 16px/184px Ôëê 31.3%
                // 20┬░ (2): top = 60% - 16px/184px Ôëê 51.3%
                // FULL (3): top = 100% - 32px/184px Ôëê 82.6%
                const leverPercent = leverPosition === 0 ? 4.3 : leverPosition === 1 ? 31.3 : leverPosition === 2 ? 51.3 : leverPosition === 3 ? 82.6 : 95;
                
                return `
                    <div class="flaps-control-realistic" data-id="${cmd.id}">
                        <div class="flaps-title">WINGS FLAPS</div>
                        <div class="flaps-mechanical-panel">
                            <div class="flaps-track">
                                <div class="flaps-speed-zone flaps-speed-110">110</div>
                                <div class="flaps-speed-zone flaps-speed-85">85</div>
                                <div class="flaps-markings">
                                    <div class="flaps-marking flaps-marking-up">
                                        <span class="flaps-label">UP</span>
                                    </div>
                                    <div class="flaps-marking flaps-marking-10">
                                        <span class="flaps-angle">10\u00b0</span>
                                    </div>
                                    <div class="flaps-marking flaps-marking-20">
                                        <span class="flaps-angle">20┬░</span>
                                    </div>
                                    <div class="flaps-marking flaps-marking-full">
                                        <span class="flaps-label">FULL</span>
                                    </div>
                                </div>
                                <div class="flaps-lever-track">
                                    <div class="flaps-lever" style="top: ${leverPercent}%"></div>
                                </div>
                            </div>
                            <div class="flaps-control-buttons">
                                <button class="flaps-btn" onclick="sendToggle('flaps_decr')">ÔêÆ</button>
                                <button class="flaps-btn" onclick="sendToggle('flaps_incr')">+</button>
                            </div>
                        </div>
                        <div class="flaps-click-zones">
                            ${optionsHtml}
                        </div>
                    </div>
                `;
            }

            return `
                <div class="selector" data-id="${cmd.id}">
                    <span class="selector-label">${cmd.name}</span>
                    <div class="selector-options">
                        ${optionsHtml}
                    </div>
                </div>
            `;
        }

        /**
         * G├®n├¿re le HTML pour un bouton Momentary (appui bref, pas de LED)
         */
        function renderMomentary(cmd) {
            return `
                <button class="btn-momentary" data-id="${cmd.id}" onclick="sendToggle('${cmd.id}')">
                    <span class="label">${cmd.name}</span>
                </button>
            `;
        }

        /**
         * G├®n├¿re le HTML pour un switch double-toggle (deux boutons c├┤te ├á c├┤te)
         * @param {string} title - Titre principal (ex: "MASTER", "AVIONICS")
         * @param {string[]} labels - Labels pour chaque levier (ex: ["ALT", "BAT"])
         * @param {Array} commands - Tableau de 2 AircraftCommand
         */
        function renderDoubleToggleSwitch(title, labels, commands) {
            const state1 = states[commands[0].id] || 0;
            const state2 = states[commands[1].id] || 0;
            const stateClass1 = state1 > 0.5 ? 'on' : 'off';
            const stateClass2 = state2 > 0.5 ? 'on' : 'off';
            // Classe de couleur selon le type de switch
            const colorClass = title === 'MASTER' ? 'switch-red' : 'switch-grey';
            
            return `
                <div class="double-toggle-switch">
                    <div class="double-toggle-title">${title}</div>
                    <div class="double-toggle-labels">
                        <span class="double-toggle-label-left">${labels[0]}</span>
                        <span class="double-toggle-label-right">${labels[1]}</span>
                    </div>
                    <div class="double-toggle-buttons">
                        <button class="double-toggle-btn ${colorClass} ${stateClass1}" 
                                data-id="${commands[0].id}" 
                                onclick="sendToggle('${commands[0].id}')">
                            <div class="indicator"></div>
                        </button>
                        <button class="double-toggle-btn ${colorClass} ${stateClass2}" 
                                data-id="${commands[1].id}" 
                                onclick="sendToggle('${commands[1].id}')">
                            <div class="indicator"></div>
                        </button>
                    </div>
                </div>
            `;
        }

        /**
         * G├®n├¿re le HTML pour un switch cylindrique (style cockpit)
         * @param {Object} cmd - AircraftCommand
         * @param {boolean} isLightSwitch - Si true, utilise un indicateur ambre au lieu de rouge
         */
        function renderCylindricalSwitch(cmd, isLightSwitch = false) {
            const state = states[cmd.id] || 0;
            const isOn = state > 0.5;
            // Couleur du levier : vert pour Pitot Heat, blanc/gris pour les autres
            const leverColor = cmd.id === 'pitot_heat' ? 'green' : 'white';
            const indicatorClass = isOn ? (isLightSwitch ? 'on-indicator-light' : 'on-indicator') : '';
            
            // LED rouge pour pitot_heat quand OAT < 4.4┬░C (├®teinte par d├®faut)
            const showOATWarning = cmd.id === 'pitot_heat' ? '<div class="oat-warning-led inactive" id="oat-warning-pitot"></div>' : '';
            
            return `
                <div class="cylindrical-switch-wrapper" data-switch-id="${cmd.id}">
                    <div class="cylindrical-switch" data-id="${cmd.id}">
                        <div class="cylindrical-switch-label">${cmd.name}</div>
                        <div class="cylindrical-switch-lever-wrapper">
                            <div class="cylindrical-switch-bezel ${indicatorClass}">
                                <div class="cylindrical-switch-lever ${leverColor} ${isOn ? 'on' : 'off'}" 
                                     data-id="${cmd.id}">
                                </div>
                            </div>
                        </div>
                        <div class="cylindrical-switch-state-wrapper">
                            <div class="cylindrical-switch-state">OFF</div>
                            ${showOATWarning}
                        </div>
                    </div>
                </div>
            `;
        }

        /**
         * G├®n├¿re le HTML pour une poign├®e rotative (frein de parking)
         * @param {Object} cmd - AircraftCommand
         */
        function renderRotatingHandle(cmd) {
            const state = states[cmd.id] || 0;
            const isOn = state > 0.5;
            // Rotation : 0┬░ quand OFF (horizontal), -90┬░ quand ON (anti-horaire vers le bas)
            const rotation = isOn ? -90 : 0;
            
            return `
                <div class="rotating-handle-container" data-id="${cmd.id}">
                    <div class="rotating-handle-panel">
                        <div class="rotating-handle-title">PARKING BRAKE</div>
                        <div class="rotating-handle-wrapper">
                            <div class="rotating-handle" 
                                 style="transform-origin: calc(100% - 16.5px) center; transform: rotate(${rotation}deg);"
                                 data-id="${cmd.id}"
                                 onclick="sendToggle('${cmd.id}')">
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }

        /**
         * G├®n├¿re le HTML pour le s├®lecteur de carburant (3 positions)
         * @param {Object} cmd - AircraftCommand
         */
        function renderFuelSelector(cmd) {
            // Essayer de restaurer depuis localStorage, sinon utiliser la valeur du serveur, sinon valeur par d├®faut
            const storedValue = localStorage.getItem('fuel_selector_position');
            let currentValue;
            if (storedValue !== null) {
                currentValue = parseInt(storedValue, 10);
            } else if (states[cmd.id] !== undefined) {
                currentValue = states[cmd.id];
            } else {
                currentValue = 1; // TAKEOFF LANDING par d├®faut
            }
            const position = Math.round(currentValue);
            // Positions : 0 = Gauche, 1 = Haut, 2 = Droite
            // Axe de rotation au centre du cercle (poign├®e)
            // Rotation pour pointer vers le haut quand position = 1 (180┬░ depuis le bas)
            // Utiliser des rotations positives (0-360) pour ├®viter les probl├¿mes de chemin le plus court
            // LEFT (0) = 90┬░, TOP (1) = 180┬░, RIGHT (2) = 270┬░
            const rotationRaw = position === 0 ? 90 : position === 1 ? 180 : 270;
            const rotation = rotationRaw; // D├®j├á positif (0-360)
            
            return `
                <div class="fuel-selector-panel" data-id="${cmd.id}">
                    <div class="rotating-handle-title">FUEL SELECTOR</div>
                    <div class="fuel-selector-wrapper">
                        <div class="fuel-selector-base">
                            <div class="fuel-selector-lever" 
                                 style="transform-origin: center 0px; transform: rotate(${rotation}deg);"
                                 data-id="${cmd.id}"
                                 data-current-rotation="${rotation}">
                            </div>
                            <div class="fuel-selector-labels">
                                <div class="fuel-selector-label-wrapper fuel-selector-left-wrapper">
                                    <div class="fuel-selector-label fuel-selector-left ${position === 0 ? 'active' : ''}" 
                                         onclick="${cmd.options[0].simEvent ? `sendSelector('${cmd.id}', '${cmd.options[0].simEvent}', 0)` : ''}">
                                        LEFT
                                    </div>
                                    <div class="fuel-selector-quantity" id="fuel-left-quantity">--</div>
                                </div>
                                <div class="fuel-selector-label fuel-selector-top ${position === 1 ? 'active' : ''}" 
                                     onclick="${cmd.options[1].simEvent ? `sendSelector('${cmd.id}', '${cmd.options[1].simEvent}', 1)` : ''}">
                                    TAKEOFF<br>LANDING
                                </div>
                                <div class="fuel-selector-label-wrapper fuel-selector-right-wrapper">
                                    <div class="fuel-selector-label fuel-selector-right ${position === 2 ? 'active' : ''}" 
                                         onclick="${cmd.options[2].simEvent ? `sendSelector('${cmd.id}', '${cmd.options[2].simEvent}', 2)` : ''}">
                                        RIGHT
                                    </div>
                                    <div class="fuel-selector-quantity" id="fuel-right-quantity">--</div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }

        /**
         * G├®n├¿re le HTML pour la jauge de trim de profondeur (verticale)
         * @param {Object} cmd - AircraftCommand (display_elevator_trim)
         */
        function renderTrimGauge(cmd) {
            return `
                <div class="trim-gauge-panel" data-id="${cmd.id}">
                    <div class="rotating-handle-title">TRIM</div>
                    <div class="trim-gauge-vertical">
                        <div class="trim-gauge-scale-vertical">
                            <div class="trim-gauge-mark-vertical trim-gauge-mark-max-vertical">+100%</div>
                            <div class="trim-gauge-mark-vertical trim-gauge-mark-takeoff-vertical">TAKEOFF<br>1.5┬░</div>
                            <div class="trim-gauge-mark-vertical trim-gauge-mark-min-vertical">-86.4%</div>
                        </div>
                        <div class="trim-gauge-bar-vertical">
                            <div class="trim-gauge-indicator-bar-vertical" id="trim-indicator-bar"></div>
                            <div class="trim-gauge-takeoff-marker-vertical" id="trim-takeoff-marker"></div>
                        </div>
                    </div>
                </div>
            `;
        }

        /**
         * G├®n├¿re le HTML pour un Potentiometer (slider 0-100%)
         */
        function renderPotentiometer(cmd) {
            const currentValue = states[cmd.id] || 0;
            const percentValue = Math.round(currentValue);
            return `
                <div class="potentiometer-control" data-cmd-id="${cmd.id}">
                    <div class="potentiometer-label">${cmd.name}</div>
                    <div class="potentiometer-value" data-cmd-id="${cmd.id}">${percentValue}%</div>
                    <div class="potentiometer-track-wrapper">
                        <div class="potentiometer-track" data-cmd-id="${cmd.id}">
                            <div class="potentiometer-fill" data-cmd-id="${cmd.id}" style="width: ${percentValue}%"></div>
                        </div>
                        <input type="range" 
                               class="potentiometer-slider" 
                               data-cmd-id="${cmd.id}"
                               min="0" 
                               max="100" 
                               step="1" 
                               value="${percentValue}"
                               aria-label="${cmd.name}">
                    </div>
                </div>
            `;
        }

        // ================================================================
        // MISE ├Ç JOUR DE L'INTERFACE
        // ================================================================

        /**
         * Met ├á jour l'├®tat visuel d'un contr├┤le
         * Appel├® quand le serveur envoie un changement d'├®tat
         *
         * @param {string} id - ID de la commande (ex: "nav_lights")
         * @param {number} value - Nouvelle valeur (0.0 ou 1.0 pour Bool, autres pour Number)
         */
        function updateControlState(id, value) {
            states[id] = value;

            // Toggle buttons
            const toggleBtn = document.querySelector(`.btn-toggle[data-id="${id}"]`);
            if (toggleBtn) {
                toggleBtn.classList.remove('on', 'off');
                toggleBtn.classList.add(value > 0.5 ? 'on' : 'off');
            }

            // Double-toggle switches
            const doubleToggleBtn = document.querySelector(`.double-toggle-btn[data-id="${id}"]`);
            if (doubleToggleBtn) {
                doubleToggleBtn.classList.remove('on', 'off');
                doubleToggleBtn.classList.add(value > 0.5 ? 'on' : 'off');
            }

            // Cylindrical switches
            const cylindricalSwitch = document.querySelector(`.cylindrical-switch[data-id="${id}"]`);
            if (cylindricalSwitch) {
                const lever = cylindricalSwitch.querySelector('.cylindrical-switch-lever');
                const bezel = cylindricalSwitch.querySelector('.cylindrical-switch-bezel');
                const stateLabel = cylindricalSwitch.querySelector('.cylindrical-switch-state');
                const isOn = value > 0.5;
                // V├®rifier si c'est un switch de lumi├¿res (dans .lights-container)
                const isLightSwitch = cylindricalSwitch.closest('.lights-container') !== null;
                
                if (lever) {
                    lever.classList.remove('on', 'off');
                    lever.classList.add(isOn ? 'on' : 'off');
                }
                if (bezel) {
                    bezel.classList.remove('on-indicator', 'on-indicator-light');
                    if (isOn) {
                        bezel.classList.add(isLightSwitch ? 'on-indicator-light' : 'on-indicator');
                    }
                }
                // Le texte reste toujours "OFF" comme dans l'avion r├®el
                // L'├®tat est indiqu├® uniquement par la position du levier
                // Pas de modification du texte stateLabel
            }

            // Selector options (including flaps-control)
            const selector = document.querySelector(`.selector[data-id="${id}"], .flaps-control[data-id="${id}"], .flaps-control-realistic[data-id="${id}"]`);
            if (selector) {
                // Mise ├á jour du levier pour le s├®lecteur r├®aliste
                if (selector.classList.contains('flaps-control-realistic')) {
                    const lever = selector.querySelector('.flaps-lever');
                    const indicator = selector.querySelector('.flaps-indicator');
                    const leverPosition = Math.round(value);
                    // Positions non-lin├®aires : 10┬░ ├á 40%, 20┬░ ├á 60%
                    // UP (0): un peu moins haut (4.3%)
                    const leverPercent = leverPosition === 0 ? 4.3 : leverPosition === 1 ? 31.3 : leverPosition === 2 ? 51.3 : leverPosition === 3 ? 82.6 : 95;
                    
                    if (lever) {
                        lever.style.top = `${leverPercent}%`;
                }
            }
            
            const options = selector.querySelectorAll('.selector-option');
            options.forEach(opt => {
                const optValue = parseFloat(opt.dataset.value);
                opt.classList.toggle('active', Math.abs(value - optValue) < 0.5);
            });
        }

        // Mise ├á jour de la poign├®e rotative (frein de parking)
        const rotatingHandle = document.querySelector(`.rotating-handle[data-id="${id}"]`);
        if (rotatingHandle) {
            const isOn = value > 0.5;
            const rotation = isOn ? -90 : 0; // Anti-horaire
            rotatingHandle.style.transformOrigin = 'calc(100% - 16.5px) center'; // Centre du cercle gris clair
            rotatingHandle.style.transform = `rotate(${rotation}deg)`;
        }

        // Mise ├á jour du s├®lecteur de carburant
        const fuelSelector = document.querySelector(`.fuel-selector-panel[data-id="${id}"]`);
        if (fuelSelector) {
            // Si value est undefined ou null, essayer de restaurer depuis localStorage
            let positionValue = value;
            if ((positionValue === undefined || positionValue === null) && id === 'fuel_selector') {
                const storedValue = localStorage.getItem('fuel_selector_position');
                if (storedValue !== null) {
                    positionValue = parseInt(storedValue, 10);
                } else {
                    positionValue = 1; // TAKEOFF LANDING par d├®faut
                }
            }
            const position = Math.round(positionValue);
            
            // Sauvegarder la position dans localStorage ├á chaque mise ├á jour
            if (id === 'fuel_selector') {
                localStorage.setItem('fuel_selector_position', position.toString());
            }
            // Rotation pour pointer vers le haut quand position = 1 (180┬░ depuis le bas)
            // Positions: 0 = LEFT (90┬░), 1 = TOP (180┬░), 2 = RIGHT (270┬░)
            const targetRotation = position === 0 ? 90 : position === 1 ? 180 : 270;
            
            const lever = fuelSelector.querySelector('.fuel-selector-lever');
            if (lever) {
                let currentRotation = parseFloat(lever.dataset.currentRotation || '0');
                
                // Normaliser la rotation actuelle entre 0 et 360
                currentRotation = ((currentRotation % 360) + 360) % 360;
                
                // D├®terminer le sens de rotation souhait├® en fonction des positions
                // Pour ├®viter que CSS choisisse le mauvais chemin (ex: 180┬░ ÔåÆ 90┬░ via 270┬░)
                let finalRotation;
                
                // Cas sp├®ciaux pour forcer le bon sens de rotation
                if (currentRotation === 180 && targetRotation === 270) {
                    // De haut (180┬░) ├á droite (270┬░) : forcer le chemin horaire direct
                    finalRotation = 270;
                } else if (currentRotation === 270 && targetRotation === 180) {
                    // De droite (270┬░) ├á haut (180┬░) : forcer le chemin anti-horaire direct
                    finalRotation = 180;
                } else if (currentRotation === 180 && targetRotation === 90) {
                    // De haut (180┬░) ├á gauche (90┬░) : forcer le chemin anti-horaire direct
                    finalRotation = 90;
                } else if (currentRotation === 90 && targetRotation === 180) {
                    // De gauche (90┬░) ├á haut (180┬░) : forcer le chemin horaire direct
                    finalRotation = 180;
                } else if (currentRotation === 90 && targetRotation === 270) {
                    // De gauche (90┬░) ├á droite (270┬░) : forcer le chemin horaire direct
                    finalRotation = 270;
                } else if (currentRotation === 270 && targetRotation === 90) {
                    // De droite (270┬░) ├á gauche (90┬░) : forcer le chemin anti-horaire direct
                    finalRotation = 90;
                } else {
                    // Pour les autres cas, utiliser le chemin le plus court
                    let diffClockwise = targetRotation - currentRotation;
                    if (diffClockwise < 0) diffClockwise += 360;
                    
                    let diffCounterClockwise = currentRotation - targetRotation;
                    if (diffCounterClockwise < 0) diffCounterClockwise += 360;
                    
                    if (diffClockwise <= diffCounterClockwise) {
                        finalRotation = currentRotation + diffClockwise;
                    } else {
                        finalRotation = currentRotation - diffCounterClockwise;
                    }
                }
                
                // Normaliser entre 0 et 360
                finalRotation = ((finalRotation % 360) + 360) % 360;
                
                lever.dataset.currentRotation = finalRotation.toString();
                lever.style.transformOrigin = 'center 0px'; // Centre du cercle noir
                lever.style.transform = `rotate(${finalRotation}deg)`;
            }
            
            // Mettre ├á jour les labels actifs
            const labels = fuelSelector.querySelectorAll('.fuel-selector-label');
            labels.forEach((label, index) => {
                label.classList.toggle('active', index === position);
            });
        }

            // === AFFICHEURS AUTOPILOT ===
            // Ces IDs correspondent aux commandes "hidden" du profil
            // Logique sp├®ciale: SPD et VS ne s'affichent que si le mode correspondant est actif
            if (id === 'display_spd') {
                const el = document.getElementById('display-spd');
                const flcActive = states['ap_flc'] > 0.5;
                if (el) el.textContent = flcActive ? Math.round(value) : '---';
            }
            if (id === 'display_hdg') {
                const el = document.getElementById('display-hdg');
                if (el) el.textContent = Math.round(value).toString().padStart(3, '0');
            }
            if (id === 'display_alt') {
                const el = document.getElementById('display-alt');
                if (el) el.textContent = Math.round(value).toString().padStart(5, '0');
            }
            if (id === 'display_vs') {
                const el = document.getElementById('display-vs');
                const vsActive = states['ap_vs'] > 0.5;
                if (el) el.textContent = vsActive ? ((value >= 0 ? '+' : '') + Math.round(value)) : '----';
            }
            // Update displays when button state changes
            if (id === 'ap_flc') {
                const el = document.getElementById('display-spd');
                const spdValue = states['display_spd'] || 0;
                if (el) el.textContent = value > 0.5 ? Math.round(spdValue) : '---';
            }
            if (id === 'ap_vs') {
                const el = document.getElementById('display-vs');
                const vsValue = states['display_vs'] || 0;
                if (el) el.textContent = value > 0.5 ? ((vsValue >= 0 ? '+' : '') + Math.round(vsValue)) : '----';
            }

            // Potentiometer controls
            const potentiometer = document.querySelector(`.potentiometer-control[data-cmd-id="${id}"]`);
            if (potentiometer) {
                const percentValue = Math.round(value);
                const valueLabel = potentiometer.querySelector('.potentiometer-value');
                const fillBar = potentiometer.querySelector('.potentiometer-fill');
                const slider = potentiometer.querySelector('.potentiometer-slider');
                
                if (valueLabel) valueLabel.textContent = `${percentValue}%`;
                if (fillBar) fillBar.style.width = `${percentValue}%`;
                if (slider) slider.value = percentValue;
            }
        }

        // ================================================================
        // ENVOI DE COMMANDES
        // ================================================================

        /**
         * Envoie une commande Toggle/Momentary au serveur
         * @param {string} id - ID de la commande (ex: "nav_lights", "hdg_inc_10")
         */
        function sendToggle(id) {
            if (!ws || ws.readyState !== WebSocket.OPEN) return;

            ws.send(JSON.stringify({
                type: 'command',
                data: { id: id }
            }));
        }

        /**
         * Envoie une commande Selector au serveur avec l'event sp├®cifique
         * @param {string} id - ID de la commande (ex: "flaps")
         * @param {string} simEvent - Event SimConnect ├á ex├®cuter (ex: "FLAPS_2")
         * @param {number} value - Valeur de la position (ex: 2)
         */
        function sendSelector(id, simEvent, value) {
            if (!ws || ws.readyState !== WebSocket.OPEN) return;

            ws.send(JSON.stringify({
                type: 'command',
                data: { id: id, simEvent: simEvent, value: value }
            }));

            // Sauvegarder la position dans localStorage pour le s├®lecteur de carburant
            if (id === 'fuel_selector') {
                localStorage.setItem('fuel_selector_position', value.toString());
            }

            // Mise ├á jour optimiste de l'UI (n'attend pas la confirmation serveur)
            updateControlState(id, value);
        }

        /**
         * Envoie une commande Potentiometer au serveur avec la valeur (0-100)
         * @param {string} id - ID de la commande (ex: "interior_panels")
         * @param {number} value - Valeur du potentiom├¿tre (0-100)
         */
        function sendPotentiometer(id, value) {
            if (!ws || ws.readyState !== WebSocket.OPEN) {
                return;
            }
            
            const message = {
                type: 'command',
                data: { id: id, value: value }
            };
            ws.send(JSON.stringify(message));
        }

        // ================================================================
        // HDG & ALT INCREMENT SELECTORS
        // ================================================================
        let hdgIncrement = 1; // Valeur par d├®faut : 1
        let altIncrement = 100; // Valeur par d├®faut : 100

        /**
         * D├®finit l'incr├®ment pour les boutons HDG (+/-)
         * @param {number} value - Valeur de l'incr├®ment (1 ou 10)
         */
        function setHdgIncrement(value) {
            hdgIncrement = value;
            // Mettre ├á jour l'apparence des boutons s├®lecteurs HDG uniquement
            document.querySelectorAll('[data-hdg-value]').forEach(btn => {
                const btnValue = parseInt(btn.dataset.hdgValue);
                if (btnValue === value) {
                    btn.classList.add('active');
                } else {
                    btn.classList.remove('active');
                }
            });
        }

        /**
         * Envoie une commande d'incr├®ment/d├®cr├®ment HDG avec l'incr├®ment s├®lectionn├®
         * @param {boolean} increment - true pour incr├®menter, false pour d├®cr├®menter
         */
        function sendHdgIncrement(increment) {
            if (hdgIncrement === 1) {
                const commandId = increment ? 'hdg_inc_1' : 'hdg_dec_1';
                sendToggle(commandId);
            } else if (hdgIncrement === 10) {
                const commandId = increment ? 'hdg_inc_10' : 'hdg_dec_10';
                sendToggle(commandId);
            }
        }

        /**
         * D├®finit l'incr├®ment pour les boutons ALT (+/-)
         * @param {number} value - Valeur de l'incr├®ment (100 ou 1000)
         */
        function setAltIncrement(value) {
            altIncrement = value;
            // Mettre ├á jour l'apparence des boutons s├®lecteurs ALT uniquement
            document.querySelectorAll('[data-alt-value]').forEach(btn => {
                const btnValue = parseInt(btn.dataset.altValue);
                if (btnValue === value) {
                    btn.classList.add('active');
                } else {
                    btn.classList.remove('active');
                }
            });
        }

        /**
         * Envoie une commande d'incr├®ment/d├®cr├®ment ALT avec l'incr├®ment s├®lectionn├®
         * @param {boolean} increment - true pour incr├®menter, false pour d├®cr├®menter
         */
        function sendAltIncrement(increment) {
            const commandId = increment 
                ? (altIncrement === 100 ? 'alt_inc_100' : 'alt_inc_1000')
                : (altIncrement === 100 ? 'alt_dec_100' : 'alt_dec_1000');
            sendToggle(commandId);
        }

        // ================================================================
        // HEARTBEAT & D├ëMARRAGE
        // ================================================================

        // Ping toutes les 30 secondes pour garder la connexion active
        setInterval(() => {
            if (ws && ws.readyState === WebSocket.OPEN) {
                ws.send(JSON.stringify({ type: 'ping' }));
            }
        }, 30000);

        // D\u00e9marrage de l'application
        connect();
