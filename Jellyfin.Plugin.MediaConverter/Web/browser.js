(function () {
    var selectedItemIds = [];

    function apiGet(path) {
        return ApiClient.getJSON(ApiClient.getUrl(path));
    }

    function apiPost(path, body) {
        return ApiClient.ajax({
            type: 'POST',
            url: ApiClient.getUrl(path),
            data: JSON.stringify(body || {}),
            contentType: 'application/json',
            dataType: 'json'
        });
    }

    function showError(context, error) {
        console.error('Media Converter: ' + context, error);
        var banner = document.getElementById('mediaConverterError');
        if (banner) {
            var detail = (error && (error.message || error.statusText)) || 'request failed';
            banner.textContent = context + ': ' + detail;
            banner.style.display = 'block';
        }
    }

    function clearError() {
        var banner = document.getElementById('mediaConverterError');
        if (banner) {
            banner.style.display = 'none';
        }
    }

    function formatBytes(bytes) {
        if (!bytes) {
            return '';
        }

        var gb = bytes / (1024 * 1024 * 1024);
        if (gb >= 1) {
            return gb.toFixed(2) + ' GB';
        }

        return (bytes / (1024 * 1024)).toFixed(0) + ' MB';
    }

    function formatMediaInfo(info) {
        if (!info) {
            return 'stats unavailable';
        }

        var parts = [];

        if (info.VideoCodec) {
            var resolution = (info.Width && info.Height) ? (' ' + info.Width + 'x' + info.Height) : '';
            parts.push(info.VideoCodec.toUpperCase() + resolution);
        }

        if (info.AudioCodec) {
            var channels = info.AudioChannels ? (' ' + info.AudioChannels + 'ch') : '';
            parts.push(info.AudioCodec.toUpperCase() + channels);
        }

        var size = formatBytes(info.FileSizeBytes);
        if (size) {
            parts.push(size);
        }

        return parts.length ? parts.join(' · ') : 'stats unavailable';
    }

    function loadMediaInfo(itemId, span) {
        span.textContent = 'Loading stats…';
        apiGet('MediaConverter/Items/' + itemId + '/MediaInfo')
            .then(function (info) {
                span.textContent = formatMediaInfo(info);
            })
            .catch(function () {
                span.textContent = '';
            });
    }

    function appendStatsSpan(row, itemId) {
        var stats = document.createElement('span');
        stats.className = 'mcStats';
        stats.style.marginLeft = '8px';
        stats.style.opacity = '0.7';
        row.appendChild(stats);
        loadMediaInfo(itemId, stats);
    }

    function formatBitrate(bitsPerSecond) {
        if (!bitsPerSecond) {
            return null;
        }

        if (bitsPerSecond >= 1000000) {
            return (bitsPerSecond / 1000000).toFixed(2) + ' Mbps';
        }

        return (bitsPerSecond / 1000).toFixed(0) + ' kbps';
    }

    function renderMediaInfoDetail(container, itemId, labelText, info) {
        container.innerHTML = '';

        if (!info) {
            container.textContent = 'Unable to read media info.';
            return;
        }

        var sourceBps = info.VideoBitRate || info.OverallBitRate;
        var videoBitrate = formatBitrate(sourceBps);
        var audioBitrate = formatBitrate(info.AudioBitRate);

        var lines = [
            'Container: ' + (info.Container || 'unknown'),
            'Video: ' + (info.VideoCodec ? info.VideoCodec.toUpperCase() : 'none') +
                (info.Width && info.Height ? (' ' + info.Width + 'x' + info.Height) : '') +
                (videoBitrate ? (' @ ' + videoBitrate) : ' (bitrate unknown)'),
            'Audio: ' + (info.AudioCodec ? info.AudioCodec.toUpperCase() : 'none') +
                (info.AudioChannels ? (' ' + info.AudioChannels + 'ch') : '') +
                (audioBitrate ? (' @ ' + audioBitrate) : ' (bitrate unknown)'),
            'File size: ' + (formatBytes(info.FileSizeBytes) || 'unknown')
        ];

        lines.forEach(function (line) {
            var lineEl = document.createElement('div');
            lineEl.textContent = line;
            container.appendChild(lineEl);
        });

        if (sourceBps) {
            var halfKbps = Math.max(1, Math.round(sourceBps / 2 / 1000));

            var halfBitrateButton = document.createElement('button');
            halfBitrateButton.setAttribute('is', 'emby-button');
            halfBitrateButton.className = 'raised';
            halfBitrateButton.style.marginTop = '4px';
            halfBitrateButton.textContent = 'Convert at ~half bitrate (' + halfKbps + ' kbps, HEVC/AV1)';
            halfBitrateButton.addEventListener('click', function () {
                openConvertDialog([itemId], labelText, info);
                document.getElementById('codecSelect').value = 'hevc';
                document.getElementById('rateControlSelect').value = 'Bitrate';
                document.getElementById('videoBitrateInput').value = halfKbps;
                updateRateControlVisibility();
            });
            container.appendChild(halfBitrateButton);
        }
    }

    // Builds a media item row (label + inline stats + a collapsible detail panel with full
    // ffprobe codec/bitrate info, toggled by clicking the label) plus the detail panel element,
    // which the caller appends as the row's sibling.
    function buildMediaRow(itemId, labelText) {
        var row = document.createElement('div');
        row.className = 'listItem';

        var label = document.createElement('span');
        label.textContent = labelText;
        label.style.cursor = 'pointer';
        label.style.textDecoration = 'underline dotted';
        label.title = 'Click for detailed codec/bitrate info';
        row.appendChild(label);

        appendStatsSpan(row, itemId);

        var detailBox = document.createElement('div');
        detailBox.style.display = 'none';
        detailBox.style.marginTop = '4px';
        detailBox.style.marginLeft = '16px';
        detailBox.style.marginBottom = '8px';
        detailBox.style.fontSize = '0.9em';
        detailBox.style.opacity = '0.85';

        label.addEventListener('click', function () {
            if (detailBox.style.display === 'none') {
                detailBox.style.display = 'block';
                detailBox.textContent = 'Loading media info…';
                apiGet('MediaConverter/Items/' + itemId + '/MediaInfo')
                    .then(function (info) {
                        renderMediaInfoDetail(detailBox, itemId, labelText, info);
                    })
                    .catch(function () {
                        detailBox.textContent = 'Unable to read media info.';
                    });
            } else {
                detailBox.style.display = 'none';
            }
        });

        return { row: row, detailBox: detailBox };
    }

    // The single item's ffprobe stats currently backing the predicted-size estimate in the
    // convert dialog; null for batch conversions or before it's loaded.
    var currentItemMediaInfo = null;

    function updateRateControlVisibility() {
        var isBitrateMode = document.getElementById('rateControlSelect').value === 'Bitrate';
        document.getElementById('videoBitrateContainer').style.display = isBitrateMode ? 'block' : 'none';
        updatePredictedSize();
    }

    function updatePredictedSize() {
        var textEl = document.getElementById('predictedSizeText');
        if (!textEl) {
            return;
        }

        if (selectedItemIds.length !== 1) {
            textEl.textContent = 'Predicted size: only available when converting a single item.';
            return;
        }

        if (!currentItemMediaInfo || !currentItemMediaInfo.DurationTicks) {
            textEl.textContent = 'Predicted size: unavailable (source duration unknown).';
            return;
        }

        if (document.getElementById('rateControlSelect').value !== 'Bitrate') {
            textEl.textContent = 'Predicted size: switch rate control to "Target average bitrate" to estimate (quality-based size depends on content).';
            return;
        }

        var videoKbps = parseInt(document.getElementById('videoBitrateInput').value, 10);
        if (!(videoKbps > 0)) {
            textEl.textContent = 'Predicted size: enter a target video bitrate above.';
            return;
        }

        var durationSeconds = currentItemMediaInfo.DurationTicks / 10000000;
        var audioCodec = document.getElementById('audioCodecSelect').value;
        var audioKbps = 0;
        var audioUnknown = false;

        if (audioCodec === 'copy') {
            if (currentItemMediaInfo.AudioBitRate) {
                audioKbps = currentItemMediaInfo.AudioBitRate / 1000;
            } else {
                audioUnknown = true;
            }
        } else {
            var enteredAudioKbps = parseInt(document.getElementById('audioBitrateInput').value, 10);
            if (enteredAudioKbps > 0) {
                audioKbps = enteredAudioKbps;
            } else {
                audioUnknown = true;
            }
        }

        var predictedBytes = ((videoKbps + audioKbps) * 1000 / 8) * durationSeconds;
        var sizeText = formatBytes(predictedBytes) || (predictedBytes.toFixed(0) + ' bytes');

        textEl.textContent = 'Predicted size: ~' + sizeText + (audioUnknown ? ' (audio bitrate unknown, actual size may be larger)' : '');
    }

    function openConvertDialog(itemIds, label, mediaInfo) {
        selectedItemIds = itemIds;
        var title = itemIds.length > 1 ? 'Convert ' + itemIds.length + ' items' : 'Convert';
        document.getElementById('convertDialogTitle').textContent = label ? title + ' – ' + label : title;
        document.getElementById('convertDialog').style.display = 'block';

        document.getElementById('rateControlSelect').value = 'Quality';
        document.getElementById('videoBitrateInput').value = '';
        updateRateControlVisibility();

        currentItemMediaInfo = null;

        if (itemIds.length === 1) {
            if (mediaInfo) {
                currentItemMediaInfo = mediaInfo;
                updatePredictedSize();
            } else {
                apiGet('MediaConverter/Items/' + itemIds[0] + '/MediaInfo')
                    .then(function (info) {
                        currentItemMediaInfo = info;
                        updatePredictedSize();
                    })
                    .catch(function () {
                        currentItemMediaInfo = null;
                        updatePredictedSize();
                    });
            }
        } else {
            updatePredictedSize();
        }
    }

    function renderLibrary(items) {
        document.getElementById('episodesSection').style.display = 'none';

        var container = document.getElementById('libraryResults');
        container.innerHTML = '';

        if (!items.length) {
            var empty = document.createElement('div');
            empty.textContent = 'No matches.';
            container.appendChild(empty);
            return;
        }

        items.forEach(function (item) {
            var row;
            var detailBox = null;

            if (item.Type === 'Series') {
                row = document.createElement('div');
                row.className = 'listItem';

                var name = document.createElement('span');
                name.textContent = item.Name + ' (' + item.Type + ')';
                row.appendChild(name);

                var browseButton = document.createElement('button');
                browseButton.setAttribute('is', 'emby-button');
                browseButton.className = 'raised';
                browseButton.textContent = 'Browse episodes';
                browseButton.addEventListener('click', function () {
                    browseSeries(item.Id, item.Name);
                });
                row.appendChild(browseButton);

                var convertAllButton = document.createElement('button');
                convertAllButton.setAttribute('is', 'emby-button');
                convertAllButton.className = 'raised';
                convertAllButton.textContent = 'Convert all episodes';
                convertAllButton.addEventListener('click', function () {
                    apiGet('MediaConverter/Series/' + item.Id + '/Episodes')
                        .then(function (episodes) {
                            clearError();
                            if (!episodes.length) {
                                showError('Convert all episodes', { message: 'No episodes found.' });
                                return;
                            }

                            openConvertDialog(episodes.map(function (e) { return e.Id; }), item.Name + ' – all episodes (' + episodes.length + ')');
                        })
                        .catch(function (error) {
                            showError('Loading episodes', error);
                        });
                });
                row.appendChild(convertAllButton);
            } else {
                var built = buildMediaRow(item.Id, item.Name + ' (' + item.Type + ')');
                row = built.row;
                detailBox = built.detailBox;

                var button = document.createElement('button');
                button.setAttribute('is', 'emby-button');
                button.className = 'raised';
                button.textContent = 'Convert';
                button.addEventListener('click', function () {
                    openConvertDialog([item.Id], item.Name);
                });
                row.appendChild(button);
            }

            container.appendChild(row);
            if (detailBox) {
                container.appendChild(detailBox);
            }
        });
    }

    // When set, the "Back" button in the episodes section goes back one level (season list)
    // instead of hiding the whole section (back to the library search results).
    var episodesBackHandler = null;

    function setBackButtonHandler(handler, label) {
        episodesBackHandler = handler;
        var backButton = document.getElementById('backToResultsButton');
        var span = backButton.querySelector('span');
        (span || backButton).textContent = label;
    }

    function groupBySeason(episodes) {
        var seasons = {};
        var order = [];
        episodes.forEach(function (episode) {
            var seasonKey = episode.SeasonNumber != null ? episode.SeasonNumber : -1;
            if (!seasons[seasonKey]) {
                seasons[seasonKey] = [];
                order.push(seasonKey);
            }

            seasons[seasonKey].push(episode);
        });

        return { seasons: seasons, order: order };
    }

    function browseSeries(seriesId, seriesName) {
        apiGet('MediaConverter/Series/' + seriesId + '/Episodes')
            .then(function (episodes) {
                clearError();
                renderSeasonList(seriesId, seriesName, episodes);
            })
            .catch(function (error) {
                showError('Loading episodes', error);
            });
    }

    function renderSeasonList(seriesId, seriesName, episodes) {
        document.getElementById('episodesSeriesName').textContent = '- ' + seriesName;
        setBackButtonHandler(null, 'Back to results');

        var container = document.getElementById('episodeResults');
        container.innerHTML = '';

        if (!episodes.length) {
            var empty = document.createElement('div');
            empty.textContent = 'No episodes found.';
            container.appendChild(empty);
            document.getElementById('episodesSection').style.display = 'block';
            return;
        }

        var convertAllRow = document.createElement('div');
        convertAllRow.className = 'listItem';
        var convertAllButton = document.createElement('button');
        convertAllButton.setAttribute('is', 'emby-button');
        convertAllButton.className = 'raised';
        convertAllButton.textContent = 'Convert all ' + episodes.length + ' episodes';
        convertAllButton.addEventListener('click', function () {
            openConvertDialog(episodes.map(function (e) { return e.Id; }), seriesName + ' – all episodes');
        });
        convertAllRow.appendChild(convertAllButton);
        container.appendChild(convertAllRow);

        var grouped = groupBySeason(episodes);

        grouped.order.forEach(function (seasonKey) {
            var seasonEpisodes = grouped.seasons[seasonKey];

            var row = document.createElement('div');
            row.className = 'listItem';

            var label = document.createElement('span');
            var seasonName = seasonKey === -1 ? 'No season' : 'Season ' + seasonKey;
            label.textContent = seasonName + ' (' + seasonEpisodes.length + ' episode' + (seasonEpisodes.length === 1 ? '' : 's') + ')';
            row.appendChild(label);

            var viewButton = document.createElement('button');
            viewButton.setAttribute('is', 'emby-button');
            viewButton.className = 'raised';
            viewButton.textContent = 'View episodes';
            viewButton.addEventListener('click', function () {
                renderSeasonEpisodes(seriesId, seriesName, seasonKey, seasonName, seasonEpisodes, episodes);
            });
            row.appendChild(viewButton);

            var convertSeasonButton = document.createElement('button');
            convertSeasonButton.setAttribute('is', 'emby-button');
            convertSeasonButton.className = 'raised';
            convertSeasonButton.textContent = 'Convert this season';
            convertSeasonButton.addEventListener('click', function () {
                openConvertDialog(seasonEpisodes.map(function (e) { return e.Id; }), seriesName + ' – ' + seasonName);
            });
            row.appendChild(convertSeasonButton);

            container.appendChild(row);
        });

        document.getElementById('episodesSection').style.display = 'block';
    }

    function renderSeasonEpisodes(seriesId, seriesName, seasonKey, seasonName, seasonEpisodes, allEpisodes) {
        document.getElementById('episodesSeriesName').textContent = '- ' + seriesName + ', ' + seasonName;
        setBackButtonHandler(function () {
            renderSeasonList(seriesId, seriesName, allEpisodes);
        }, 'Back to seasons');

        var container = document.getElementById('episodeResults');
        container.innerHTML = '';

        var convertSeasonRow = document.createElement('div');
        convertSeasonRow.className = 'listItem';
        var convertSeasonButton = document.createElement('button');
        convertSeasonButton.setAttribute('is', 'emby-button');
        convertSeasonButton.className = 'raised';
        convertSeasonButton.textContent = 'Convert all ' + seasonEpisodes.length + ' episodes in this season';
        convertSeasonButton.addEventListener('click', function () {
            openConvertDialog(seasonEpisodes.map(function (e) { return e.Id; }), seriesName + ' – ' + seasonName);
        });
        convertSeasonRow.appendChild(convertSeasonButton);
        container.appendChild(convertSeasonRow);

        seasonEpisodes.forEach(function (episode) {
            var seasonEpisode = (episode.SeasonNumber != null && episode.EpisodeNumber != null)
                ? 'S' + episode.SeasonNumber + 'E' + episode.EpisodeNumber + ' - '
                : '';
            var built = buildMediaRow(episode.Id, seasonEpisode + episode.Name);

            var button = document.createElement('button');
            button.setAttribute('is', 'emby-button');
            button.className = 'raised';
            button.textContent = 'Convert';
            button.addEventListener('click', function () {
                openConvertDialog([episode.Id], episode.Name);
            });
            built.row.appendChild(button);

            container.appendChild(built.row);
            container.appendChild(built.detailBox);
        });

        document.getElementById('episodesSection').style.display = 'block';
    }

    function buildSideBySidePlayers(job) {
        var wrapper = document.createElement('div');
        wrapper.style.display = 'flex';
        wrapper.style.flexWrap = 'wrap';
        wrapper.style.gap = '12px';
        wrapper.style.marginTop = '8px';

        function buildPlayerBlock(labelText, streamPath) {
            var block = document.createElement('div');
            block.style.flex = '1 1 280px';
            block.style.maxWidth = '400px';

            var label = document.createElement('div');
            label.textContent = labelText;
            label.style.marginBottom = '4px';
            block.appendChild(label);

            var video = document.createElement('video');
            video.controls = true;
            video.preload = 'metadata';
            video.style.width = '100%';
            video.style.background = '#000';
            video.src = ApiClient.getUrl(streamPath);
            block.appendChild(video);

            return { block: block, video: video };
        }

        var originalPlayer = buildPlayerBlock('Original', 'MediaConverter/Jobs/' + job.Id + '/Stream/Original');
        var variantPlayer = buildPlayerBlock('New variant', 'MediaConverter/Jobs/' + job.Id + '/Stream/Variant');

        wrapper.appendChild(originalPlayer.block);
        wrapper.appendChild(variantPlayer.block);

        var controlsRow = document.createElement('div');
        controlsRow.style.width = '100%';

        var restartButton = document.createElement('button');
        restartButton.setAttribute('is', 'emby-button');
        restartButton.className = 'raised';
        restartButton.textContent = 'Restart both & play';
        restartButton.addEventListener('click', function () {
            [originalPlayer.video, variantPlayer.video].forEach(function (video) {
                video.currentTime = 0;
            });
            [originalPlayer.video.play(), variantPlayer.video.play()].forEach(function (playPromise) {
                if (playPromise && playPromise.catch) {
                    playPromise.catch(function () {});
                }
            });
        });
        controlsRow.appendChild(restartButton);

        var pauseButton = document.createElement('button');
        pauseButton.setAttribute('is', 'emby-button');
        pauseButton.className = 'raised';
        pauseButton.textContent = 'Pause both';
        pauseButton.addEventListener('click', function () {
            originalPlayer.video.pause();
            variantPlayer.video.pause();
        });
        controlsRow.appendChild(pauseButton);

        wrapper.appendChild(controlsRow);

        return wrapper;
    }

    function renderVariantComparison(container, job, compare) {
        container.innerHTML = '';

        var originalBlock = document.createElement('div');
        originalBlock.textContent = 'Original: ' + formatMediaInfo(compare.Original);
        container.appendChild(originalBlock);

        var variantBlock = document.createElement('div');
        variantBlock.textContent = 'New variant: ' + formatMediaInfo(compare.Variant);
        container.appendChild(variantBlock);

        var playButton = document.createElement('button');
        playButton.setAttribute('is', 'emby-button');
        playButton.className = 'raised';
        playButton.style.marginTop = '4px';
        playButton.textContent = 'Play side by side';

        var playersContainer = document.createElement('div');
        playersContainer.style.display = 'none';

        playButton.addEventListener('click', function () {
            var isHidden = playersContainer.style.display === 'none';
            if (isHidden) {
                if (!playersContainer.hasChildNodes()) {
                    playersContainer.appendChild(buildSideBySidePlayers(job));
                }
                playersContainer.style.display = 'block';
                playButton.textContent = 'Hide players';
            } else {
                playersContainer.querySelectorAll('video').forEach(function (video) {
                    video.pause();
                });
                playersContainer.style.display = 'none';
                playButton.textContent = 'Play side by side';
            }
        });

        container.appendChild(playButton);
        container.appendChild(playersContainer);

        var keepVariantButton = document.createElement('button');
        keepVariantButton.setAttribute('is', 'emby-button');
        keepVariantButton.className = 'raised';
        keepVariantButton.textContent = 'Keep new variant (delete original)';
        keepVariantButton.addEventListener('click', function () {
            if (!window.confirm('Delete the original file and keep the new variant?')) {
                return;
            }

            apiPost('MediaConverter/Jobs/' + job.Id + '/KeepVariant')
                .then(function () {
                    clearError();
                    refreshJobs();
                })
                .catch(function (error) {
                    showError('Keeping new variant', error);
                });
        });

        var keepOriginalButton = document.createElement('button');
        keepOriginalButton.setAttribute('is', 'emby-button');
        keepOriginalButton.className = 'raised';
        keepOriginalButton.textContent = 'Keep original (delete new variant)';
        keepOriginalButton.addEventListener('click', function () {
            if (!window.confirm('Delete the new variant and keep the original file?')) {
                return;
            }

            apiPost('MediaConverter/Jobs/' + job.Id + '/KeepOriginal')
                .then(function () {
                    clearError();
                    refreshJobs();
                })
                .catch(function (error) {
                    showError('Keeping original', error);
                });
        });

        container.appendChild(keepVariantButton);
        container.appendChild(keepOriginalButton);
    }

    function buildVariantReviewPanel(job) {
        var panel = document.createElement('div');
        panel.className = 'listItem';
        panel.style.paddingLeft = '16px';

        var compareButton = document.createElement('button');
        compareButton.setAttribute('is', 'emby-button');
        compareButton.className = 'raised';
        compareButton.textContent = 'Compare original vs. new variant';

        var resultsBox = document.createElement('div');
        resultsBox.style.marginTop = '8px';

        compareButton.addEventListener('click', function () {
            compareButton.disabled = true;
            apiGet('MediaConverter/Jobs/' + job.Id + '/Compare')
                .then(function (compare) {
                    clearError();
                    renderVariantComparison(resultsBox, job, compare);
                })
                .catch(function (error) {
                    showError('Comparing variant', error);
                })
                .then(function () {
                    compareButton.disabled = false;
                });
        });

        panel.appendChild(compareButton);
        panel.appendChild(resultsBox);
        return panel;
    }

    function buildProgressBar(percent) {
        var track = document.createElement('div');
        track.style.display = 'inline-block';
        track.style.verticalAlign = 'middle';
        track.style.marginLeft = '8px';
        track.style.width = '160px';
        track.style.height = '8px';
        track.style.borderRadius = '4px';
        track.style.background = 'rgba(255, 255, 255, 0.15)';
        track.style.overflow = 'hidden';

        var fill = document.createElement('div');
        fill.style.height = '100%';
        fill.style.background = '#00a4dc';
        fill.style.width = Math.max(0, Math.min(100, percent)) + '%';
        fill.style.transition = 'width 0.3s ease';

        track.appendChild(fill);
        return track;
    }

    function buildJobRow(job) {
        var row = document.createElement('div');
        row.className = 'listItem';

        var label = document.createElement('span');
        label.textContent = job.SourcePath + ' - ' + job.Status + ' (' + Math.round(job.ProgressPercent) + '%)';
        row.appendChild(label);

        if (job.Status === 'Running') {
            row.appendChild(buildProgressBar(job.ProgressPercent));
        }

        if (job.Status === 'Queued' || job.Status === 'Running') {
            var cancelButton = document.createElement('button');
            cancelButton.setAttribute('is', 'emby-button');
            cancelButton.className = 'raised';
            cancelButton.textContent = 'Cancel';
            cancelButton.addEventListener('click', function () {
                apiPost('MediaConverter/Jobs/' + job.Id + '/Cancel').catch(function (error) {
                    showError('Cancelling job', error);
                });
            });
            row.appendChild(cancelButton);
        }

        return row;
    }

    function jobSignature(job) {
        return [job.Status, Math.round(job.ProgressPercent), job.VariantResolution].join('|');
    }

    // Keyed by job id: { signature, row, panel }. Reused across polls so that a job whose data
    // hasn't changed (e.g. a completed job awaiting a variant decision) keeps its existing DOM -
    // rebuilding it every poll was wiping out the comparison results panel and would also reset
    // any embedded <video> players.
    var jobRenderCache = {};

    function renderJobs(jobs) {
        var container = document.getElementById('jobResults');
        var seenIds = {};

        jobs.forEach(function (job) {
            seenIds[job.Id] = true;
            var signature = jobSignature(job);
            var cached = jobRenderCache[job.Id];

            if (!cached || cached.signature !== signature) {
                if (cached) {
                    cached.row.remove();
                    if (cached.panel) {
                        cached.panel.remove();
                    }
                }

                var panel = (job.Mode === 'Variant' && job.Status === 'Completed' && job.VariantResolution === 'PendingReview')
                    ? buildVariantReviewPanel(job)
                    : null;

                cached = { signature: signature, row: buildJobRow(job), panel: panel };
                jobRenderCache[job.Id] = cached;
            }

            container.appendChild(cached.row);
            if (cached.panel) {
                container.appendChild(cached.panel);
            }
        });

        Object.keys(jobRenderCache).forEach(function (id) {
            if (!seenIds[id]) {
                jobRenderCache[id].row.remove();
                if (jobRenderCache[id].panel) {
                    jobRenderCache[id].panel.remove();
                }
                delete jobRenderCache[id];
            }
        });
    }

    function refreshJobs() {
        apiGet('MediaConverter/Jobs')
            .then(renderJobs)
            .catch(function (error) {
                showError('Loading jobs', error);
            });
    }

    function search() {
        var searchTerm = document.getElementById('searchTerm').value || '';
        apiGet('MediaConverter/Library?searchTerm=' + encodeURIComponent(searchTerm))
            .then(function (items) {
                clearError();
                renderLibrary(items);
            })
            .catch(function (error) {
                showError('Searching library', error);
            });
    }

    function init() {
        document.getElementById('searchButton').addEventListener('click', search);

        document.getElementById('backToResultsButton').addEventListener('click', function () {
            if (episodesBackHandler) {
                episodesBackHandler();
            } else {
                document.getElementById('episodesSection').style.display = 'none';
            }
        });

        document.getElementById('rateControlSelect').addEventListener('change', updateRateControlVisibility);
        document.getElementById('videoBitrateInput').addEventListener('input', updatePredictedSize);
        document.getElementById('audioCodecSelect').addEventListener('change', updatePredictedSize);
        document.getElementById('audioBitrateInput').addEventListener('input', updatePredictedSize);

        document.getElementById('startConversionButton').addEventListener('click', function () {
            if (!selectedItemIds.length) {
                return;
            }

            var audioBitrateValue = document.getElementById('audioBitrateInput').value;
            var resolutionValue = document.getElementById('resolutionSelect').value;
            var videoBitrateValue = document.getElementById('videoBitrateInput').value;
            var maxVideoBitrateValue = document.getElementById('maxVideoBitrateInput').value;

            apiPost('MediaConverter/Convert/Batch', {
                ItemIds: selectedItemIds,
                Container: document.getElementById('containerSelect').value,
                VideoCodec: document.getElementById('codecSelect').value,
                Quality: parseInt(document.getElementById('qualityInput').value, 10),
                RateControlMode: document.getElementById('rateControlSelect').value,
                VideoBitrateKbps: videoBitrateValue ? parseInt(videoBitrateValue, 10) : null,
                MaxVideoBitrateKbps: maxVideoBitrateValue ? parseInt(maxVideoBitrateValue, 10) : null,
                Mode: document.getElementById('modeSelect').value,
                Preset: document.getElementById('presetSelect').value || null,
                ScaleHeight: resolutionValue ? parseInt(resolutionValue, 10) : null,
                AudioCodec: document.getElementById('audioCodecSelect').value,
                AudioBitrateKbps: audioBitrateValue ? parseInt(audioBitrateValue, 10) : null,
                SubtitleMode: document.getElementById('subtitleModeSelect').value,
                FfmpegArgsOverride: document.getElementById('ffmpegOverrideInput').value || null
            }).then(function () {
                clearError();
                document.getElementById('convertDialog').style.display = 'none';
                refreshJobs();
            }).catch(function (error) {
                showError('Starting conversion', error);
            });
        });

        refreshJobs();
        setInterval(refreshJobs, 3000);
    }

    init();

    // Belt-and-braces: some Jellyfin web client versions re-fire this custom event
    // on subsequent navigations to the (cached) page without re-running its scripts.
    document.addEventListener('pageshow', refreshJobs);
})();
