/**
 * リアルタイム進捗通知 (SSE Stream) を用いた汎用インポート処理スクリプト
 * @param {Event} event フォーム送信イベント
 * @param {string} endpointUrl ストリーミングアクションのURL
 */
async function submitImportStream(event, endpointUrl) {
    event.preventDefault();
    var form = event.target;
    var formData = new FormData(form);

    var formArea = form.querySelector('#importFormArea') || form.querySelector('.import-form-area');
    var footerArea = form.querySelector('#importFooter') || form.querySelector('.modal-footer');
    var progressArea = form.querySelector('#importProgressArea') || form.querySelector('.import-progress-area');
    var selectionArea = form.querySelector('#importSelectionArea');
    var closeBtnArea = form.querySelector('#importCloseBtn') || form.querySelector('.import-close-btn');

    if (formArea) formArea.classList.add('d-none');
    if (selectionArea) selectionArea.classList.add('d-none');
    if (footerArea) footerArea.classList.add('d-none');
    if (progressArea) progressArea.classList.remove('d-none');

    var pBar = form.querySelector('#importProgressBar') || progressArea.querySelector('.progress-bar');
    var pCount = form.querySelector('#importCountText') || progressArea.querySelector('.import-count-text');
    var pKey = form.querySelector('#importCurrentKeyText') || progressArea.querySelector('.import-key-text');
    var pAlert = form.querySelector('#importAlert') || progressArea.querySelector('.alert');

    try {
        var response = await fetch(endpointUrl, {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            throw new Error('サーバー通信エラー: ' + response.statusText);
        }

        var reader = response.body.getReader();
        var decoder = new TextDecoder();
        var buffer = '';

        while (true) {
            var { done, value } = await reader.read();
            if (done) break;

            buffer += decoder.decode(value, { stream: true });
            var lines = buffer.split('\n\n');
            buffer = lines.pop();

            for (var line of lines) {
                line = line.trim();
                if (line.startsWith('data: ')) {
                    var dataStr = line.substring(6);
                    try {
                        var data = JSON.parse(dataStr);

                        var pTitle = form.querySelector('#importStatusTitle') || progressArea.querySelector('#importStatusTitle');
                        var pPerf = form.querySelector('#importPerfText') || progressArea.querySelector('#importPerfText');

                        if (data.status === 'need_selection') {
                            if (progressArea) progressArea.classList.add('d-none');
                            var selectionArea = form.querySelector('#importSelectionArea');
                            var selectionFields = form.querySelector('#selectionFields');
                            if (selectionArea && selectionFields) {
                                selectionFields.innerHTML = '';
                                var missing = data.missing || [];
                                var opts = window.masterOptions || {};

                                missing.forEach(function (key) {
                                    var div = document.createElement('div');
                                    div.className = 'mb-3';

                                    var label = document.createElement('label');
                                    label.className = 'form-label text-white fw-semibold';

                                    var select = document.createElement('select');
                                    select.className = 'form-select bg-secondary text-white border-0';

                                    if (key === 'freightTable') {
                                        label.innerText = '対象の料金表を選択してください';
                                        select.name = 'defaultFreightTableId';
                                        (opts.freightTables || []).forEach(function (item) {
                                            var opt = document.createElement('option');
                                            opt.value = item.id;
                                            opt.innerText = item.name;
                                            select.appendChild(opt);
                                        });
                                    } else if (key === 'carrier') {
                                        label.innerText = '対象の運送会社を選択してください';
                                        select.name = 'defaultCarrierId';
                                        (opts.carriers || []).forEach(function (item) {
                                            var opt = document.createElement('option');
                                            opt.value = item.id;
                                            opt.innerText = item.name;
                                            select.appendChild(opt);
                                        });
                                    } else if (key === 'shipper') {
                                        label.innerText = '対象の荷主を選択してください';
                                        select.name = 'defaultShipperId';
                                        (opts.shippers || []).forEach(function (item) {
                                            var opt = document.createElement('option');
                                            opt.value = item.id;
                                            opt.innerText = item.name;
                                            select.appendChild(opt);
                                        });
                                    } else if (key === 'warehouse') {
                                        label.innerText = '対象の倉庫を選択してください';
                                        select.name = 'defaultWarehouseId';
                                        (opts.warehouses || []).forEach(function (item) {
                                            var opt = document.createElement('option');
                                            opt.value = item.id;
                                            opt.innerText = item.name;
                                            select.appendChild(opt);
                                        });
                                    } else if (key === 'shippingClass') {
                                        label.innerText = '対象の出荷区分を選択してください';
                                        select.name = 'defaultShippingClassId';
                                        (opts.shippingClasses || []).forEach(function (item) {
                                            var opt = document.createElement('option');
                                            opt.value = item.id;
                                            opt.innerText = item.name;
                                            select.appendChild(opt);
                                        });
                                    }

                                    div.appendChild(label);
                                    div.appendChild(select);
                                    selectionFields.appendChild(div);
                                });

                                selectionArea.classList.remove('d-none');
                            }
                            return;
                        } else if (data.status === 'phase') {
                            if (pTitle && data.title) pTitle.innerText = data.title;
                            if (pKey && data.message) pKey.innerText = data.message;
                        } else if (data.status === 'start' || data.status === 'processing') {
                            var pct = data.total > 0 ? Math.floor((data.current / data.total) * 100) : 0;
                            if (pBar) {
                                pBar.style.width = pct + '%';
                                pBar.setAttribute('aria-valuenow', pct);
                            }
                            if (pTitle && data.title) pTitle.innerText = data.title;
                            if (pCount) {
                                var text = data.current.toLocaleString() + ' / ' + data.total.toLocaleString() + ' 件 (' + pct + '%)';
                                if (data.speed != null && data.elapsed != null) {
                                    text += ' | ' + data.speed.toLocaleString() + ' 件/秒 (' + data.elapsed.toFixed(1) + '秒)';
                                }
                                pCount.innerText = text;
                            }
                            if (pKey) {
                                pKey.innerText = data.currentKey ? '処理中の対象データ: ' + data.currentKey : 'データ処理中...';
                            }
                        } else if (data.status === 'completed') {
                            if (pBar) {
                                pBar.style.width = '100%';
                                pBar.classList.remove('progress-bar-animated', 'progress-bar-striped');
                                pBar.classList.add('bg-success');
                            }
                            if (pTitle) pTitle.innerText = 'インポート処理完了';
                            if (pCount) {
                                var text = data.total.toLocaleString() + ' / ' + data.total.toLocaleString() + ' 件 (100%)';
                                if (data.elapsed != null) {
                                    text += ' | 全処理時間: ' + data.elapsed.toFixed(1) + '秒 (' + Math.round(data.total / Math.max(data.elapsed, 0.1)).toLocaleString() + ' 件/秒)';
                                }
                                pCount.innerText = text;
                            }
                            if (pKey) pKey.innerText = 'すべてのレコードの更新・書き込みが完了しました。';
                            if (pAlert) {
                                pAlert.className = 'alert alert-success mt-3';
                                pAlert.innerHTML = '<i class="bi bi-check-circle me-1"></i> ' + data.message;
                                pAlert.classList.remove('d-none');
                            }
                            setTimeout(function () { location.reload(); }, 2000);
                        } else if (data.status === 'error') {
                            if (pBar) {
                                pBar.classList.remove('progress-bar-animated', 'progress-bar-striped');
                                pBar.classList.add('bg-danger');
                            }
                            if (pTitle) {
                                pTitle.innerText = 'インポート処理エラー';
                                pTitle.className = 'mb-2 fw-semibold text-danger';
                            }
                            if (pAlert) {
                                pAlert.className = 'alert alert-danger mt-3';
                                pAlert.innerHTML = '<i class="bi bi-exclamation-triangle me-1"></i> ' + data.message;
                                pAlert.classList.remove('d-none');
                            }
                            if (footerArea) footerArea.classList.remove('d-none');
                            if (closeBtnArea) closeBtnArea.classList.remove('d-none');
                        }
                    } catch (e) {
                        console.error('Progress JSON Parse Error:', e);
                    }
                }
            }
        }
    } catch (err) {
        if (pAlert) {
            pAlert.className = 'alert alert-danger mt-3';
            pAlert.innerHTML = '<i class="bi bi-exclamation-triangle me-1"></i> 通信エラー: ' + err.message;
            pAlert.classList.remove('d-none');
        }
        if (closeBtnArea) closeBtnArea.classList.remove('d-none');
    }
}
