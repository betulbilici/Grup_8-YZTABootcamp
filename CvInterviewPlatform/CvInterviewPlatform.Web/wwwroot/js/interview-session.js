// Mülakat oturumu (Session.cshtml) sayfasına özel istemci mantığı.
// Sunucudan gelen değerler Razor tarafından üretilmez; #interviewSessionRoot
// elemanının data-* attribute'larından okunur (bkz. Session.cshtml).
document.addEventListener("DOMContentLoaded", function () {
    var rawDiv = document.getElementById("raw-evaluation");
    var targetDiv = document.getElementById("markdown-evaluation");
    if (rawDiv && targetDiv) {
        var markdownText = rawDiv.textContent;
        targetDiv.innerHTML = marked.parse(markdownText);
    }

    var sessionRoot = document.getElementById("interviewSessionRoot");
    var sessionId = sessionRoot ? sessionRoot.dataset.sessionId : "";
    var currentQuestionNumber = sessionRoot ? parseInt(sessionRoot.dataset.currentQuestionNumber, 10) : 0;
    var isCompleted = sessionRoot ? sessionRoot.dataset.isCompleted === "true" : false;
    var jobTitle = sessionRoot ? sessionRoot.dataset.jobTitle : "";
    var timeLimitSeconds = sessionRoot ? parseInt(sessionRoot.dataset.timeLimitSeconds, 10) : 0;

    // --- SESLİ MÜLAKAT ENTEGRASYONU (TTS & STT) & SÜREÇ SAYAÇ ---
    let speechUtterance = null;
    let isSpeaking = false;
    let recognition = null;
    let isRecording = false;
    let timerInterval = null;

    function timerStorageKey() {
        return "timer_" + sessionId + "_" + currentQuestionNumber;
    }

    if (!isCompleted && timeLimitSeconds > 0) {
        // Mülakat Geri Sayım Sayacı (Cheat-Proof LocalStorage Destekli)
        const storageKey = timerStorageKey();
        let remainingTime = localStorage.getItem(storageKey);
        if (remainingTime === null) {
            remainingTime = timeLimitSeconds;
            localStorage.setItem(storageKey, remainingTime);
        } else {
            remainingTime = parseInt(remainingTime, 10);
        }

        const warningThreshold = Math.ceil(timeLimitSeconds * 0.2);
        const timerText = document.getElementById("countdownTimer");
        const timerContainer = document.getElementById("timerContainer");
        const timerIcon = document.getElementById("timerIcon");
        const timerWarningLabel = document.getElementById("timerWarningLabel");
        const timerAnnouncement = document.getElementById("timerAnnouncement");
        let warningAnnounced = false;
        const warningLabelText = warningThreshold >= 60
            ? `Son ${Math.round(warningThreshold / 60)} dakika`
            : `Son ${warningThreshold} saniye`;

        function updateTimerDisplay() {
            const minutes = Math.floor(remainingTime / 60);
            const seconds = remainingTime % 60;
            timerText.textContent = `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;

            // Kalan süre toplam sürenin %20'sinin altına düşünce uyarı (renk + ikon + metin).
            // Rengin tek başına bilgi taşımaması için (WCAG 1.4.1) ikon ve metin de ekleniyor;
            // ekran okuyucuya ise sadece eşik geçildiği AN bir kez duyuruluyor, her saniye değil.
            if (remainingTime <= warningThreshold) {
                timerContainer.classList.remove("text-secondary", "border-secondary-subtle");
                timerContainer.classList.add("timer-warning", "border-danger");
                if (timerIcon) {
                    timerIcon.classList.remove("bi-hourglass-split", "text-primary");
                    timerIcon.classList.add("bi-exclamation-triangle-fill", "text-danger");
                }
                if (timerWarningLabel) {
                    timerWarningLabel.textContent = warningLabelText;
                    timerWarningLabel.classList.remove("d-none");
                }
                if (timerAnnouncement && !warningAnnounced) {
                    warningAnnounced = true;
                    timerAnnouncement.textContent = warningLabelText + " kaldı.";
                }
            }
        }

        updateTimerDisplay();

        timerInterval = setInterval(function () {
            if (remainingTime > 0) {
                remainingTime--;
                localStorage.setItem(storageKey, remainingTime);
                updateTimerDisplay();
            } else {
                clearInterval(timerInterval);
                localStorage.removeItem(storageKey);

                // Süre dolduğunda boş cevaba izin verme, placeholder doldur
                const answerTextarea = document.getElementById("answer");
                if (answerTextarea) {
                    if (!answerTextarea.value.trim()) {
                        answerTextarea.value = "[Mülakat süresi dolduğu için aday bu soruya cevap yazamadı.]";
                    }
                }

                // Formu gönder
                const answerForm = document.getElementById("answerForm");
                if (answerForm) {
                    alert("Süreniz doldu! Cevabınız otomatik olarak kaydedilip bir sonraki soruya geçiliyor.");
                    answerForm.submit();
                }
            }
        }, 1000);
    }

    // 1. Text-to-Speech (Yapay Zeka Soru Okuma)
    // Önce Azure Neural TTS deneniyor (StartInterview/SubmitAnswer sırasında önceden
    // üretilip önbelleğe alınmış, doğal Türkçe ses — bkz. InterviewController.QuestionAudio).
    // Önbellekte yoksa veya oynatma tarayıcı tarafından engellenirse (autoplay policy),
    // tarayıcının kendi speechSynthesis'ine (daha düşük kalite ama her zaman çalışan) düşülüyor.
    var ttsAudio = null;

    function speakBrowserFallback(text) {
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel(); // Mevcut tüm konuşmaları iptal et

            speechUtterance = new SpeechSynthesisUtterance(text);
            speechUtterance.lang = 'tr-TR';

            // Türkçe ses seçimi yapmaya çalışıyoruz
            const voices = window.speechSynthesis.getVoices();
            const trVoice = voices.find(voice => voice.lang.includes('tr'));
            if (trVoice) {
                speechUtterance.voice = trVoice;
            }

            speechUtterance.onstart = function () {
                setTtsState(true);
            };

            speechUtterance.onend = function () {
                setTtsState(false);
            };

            speechUtterance.onerror = function () {
                setTtsState(false);
            };

            window.speechSynthesis.speak(speechUtterance);
        }
    }

    function speakText(text) {
        var audioUrl = "/Interview/QuestionAudio?id=" + encodeURIComponent(sessionId) + "&questionNumber=" + currentQuestionNumber;
        ttsAudio = new Audio(audioUrl);
        ttsAudio.onplay = function () {
            setTtsState(true);
        };
        ttsAudio.onended = function () {
            setTtsState(false);
        };
        ttsAudio.onerror = function () {
            // Önbellekte ses yok (404) ya da başka bir hata — tarayıcı sesine düş
            speakBrowserFallback(text);
        };
        var playPromise = ttsAudio.play();
        if (playPromise) {
            playPromise.catch(function () {
                // Otomatik oynatma tarayıcı tarafından engellendi (autoplay policy) —
                // kullanıcı "Sesli Oku"ya manuel bastığında bu bir kullanıcı jesti
                // olduğu için engellenmeyecek.
                setTtsState(false);
            });
        }
    }

    function stopSpeaking() {
        if (ttsAudio) {
            ttsAudio.pause();
            ttsAudio.currentTime = 0;
        }
        if ('speechSynthesis' in window) {
            window.speechSynthesis.cancel();
        }
        setTtsState(false);
    }

    function setTtsState(speaking) {
        isSpeaking = speaking;
        const ttsBtn = document.getElementById("ttsBtn");
        if (ttsBtn) {
            ttsBtn.setAttribute("aria-pressed", speaking ? "true" : "false");
            if (speaking) {
                ttsBtn.innerHTML = '<i class="bi bi-volume-mute-fill fs-5"></i>';
                ttsBtn.classList.remove("btn-outline-primary");
                ttsBtn.classList.add("btn-primary");
                ttsBtn.setAttribute("aria-label", "Okumayı durdur");
            } else {
                ttsBtn.innerHTML = '<i class="bi bi-volume-up-fill fs-5"></i>';
                ttsBtn.classList.remove("btn-primary");
                ttsBtn.classList.add("btn-outline-primary");
                ttsBtn.setAttribute("aria-label", "Soruyu sesli oku");
            }
        }
    }

    // Sayfa yüklendiğinde sorunun otomatik okunması (Mülakat devam ediyorsa)
    const questionTextElement = document.getElementById("questionText");
    let autoPlayTriggered = false;

    function triggerAutoPlay() {
        if (autoPlayTriggered) return;
        autoPlayTriggered = true;
        speakText(questionTextElement.textContent.trim());
    }

    if (questionTextElement) {
        setTimeout(() => {
            triggerAutoPlay();
        }, 800);
    }

    const ttsBtn = document.getElementById("ttsBtn");
    if (ttsBtn && questionTextElement) {
        ttsBtn.addEventListener("click", function () {
            var isCurrentlySpeaking = (ttsAudio && !ttsAudio.paused) || (window.speechSynthesis && window.speechSynthesis.speaking);
            if (isCurrentlySpeaking) {
                stopSpeaking();
            } else {
                speakText(questionTextElement.textContent.trim());
            }
        });
    }

    // 2. Speech-to-Text (Konuşarak Cevap Verme)
    // interimResults=true: kullanıcı konuşurken metin anlık (kelime kelime) güncelleniyor,
    // önceden sadece cümle tamamlanip "final" sayılınca yazı beliriyordu — bu da
    // konuşmayla yazı arasında gözle görülür bir gecikme hissi veriyordu.
    let sttBaseText = '';
    let sttFinalTranscript = '';

    if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
        const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
        recognition = new SpeechRecognition();
        recognition.continuous = true;
        recognition.interimResults = true;
        recognition.lang = 'tr-TR';

        recognition.onstart = function () {
            setSttState(true);
        };

        recognition.onend = function () {
            setSttState(false);
        };

        recognition.onerror = function (event) {
            console.error("Ses tanıma hatası:", event.error);
            setSttState(false);
        };

        recognition.onresult = function (event) {
            const answerArea = document.getElementById("answer");
            if (answerArea) {
                let interimTranscript = '';
                for (let i = event.resultIndex; i < event.results.length; ++i) {
                    if (event.results[i].isFinal) {
                        sttFinalTranscript += event.results[i][0].transcript;
                    } else {
                        interimTranscript += event.results[i][0].transcript;
                    }
                }
                answerArea.value = sttBaseText + sttFinalTranscript + interimTranscript;
            }
        };
    } else {
        // Tarayıcı desteklemiyorsa butonu gizle
        const sttBtn = document.getElementById("sttBtn");
        if (sttBtn) {
            sttBtn.classList.add("d-none");
        }
    }

    function setSttState(recording) {
        isRecording = recording;
        const sttBtn = document.getElementById("sttBtn");
        const recordingAnnouncement = document.getElementById("recordingAnnouncement");
        if (sttBtn) {
            sttBtn.setAttribute("aria-pressed", recording ? "true" : "false");
            if (recording) {
                sttBtn.classList.add("recording");
                sttBtn.innerHTML = '<i class="bi bi-mic-mute-fill me-1"></i> Kaydı Durdur';
                sttBtn.setAttribute("aria-label", "Kaydı durdur");
            } else {
                sttBtn.classList.remove("recording");
                sttBtn.innerHTML = '<i class="bi bi-mic-fill me-1"></i> Sesli Yanıtla';
                sttBtn.setAttribute("aria-label", "Sesli yanıt ver");
            }
        }
        if (recordingAnnouncement) {
            recordingAnnouncement.textContent = recording ? "Kayıt başladı." : "Kayıt durdu.";
        }
    }

    const sttBtn = document.getElementById("sttBtn");
    if (sttBtn && recognition) {
        sttBtn.addEventListener("click", function () {
            if (isRecording) {
                recognition.stop();
            } else {
                stopSpeaking(); // Kayıt başlarken hoparlörü sustur (geri beslemeyi önlemek için)
                const answerArea = document.getElementById("answer");
                sttBaseText = answerArea && answerArea.value
                    ? (answerArea.value.endsWith(' ') ? answerArea.value : answerArea.value + ' ')
                    : '';
                sttFinalTranscript = '';
                recognition.start();
            }
        });
    }

    // Form gönderildiğinde yükleniyor animasyonunu açıyoruz
    var form = document.getElementById("answerForm");
    if (form) {
        form.addEventListener("submit", function () {
            // Mülakat seslerini ve kaydını kapat
            stopSpeaking();
            if (recognition && isRecording) {
                recognition.stop();
            }

            // Sayaç temizleme
            if (!isCompleted) {
                localStorage.removeItem(timerStorageKey());
                if (timerInterval) {
                    clearInterval(timerInterval);
                }
            }

            // Butonu devre dışı bırak ve gizle
            var submitBtn = document.getElementById("submitBtn");
            if (submitBtn) {
                submitBtn.disabled = true;
                submitBtn.classList.add("d-none");
            }
            // Form elemanlarını gizle
            var answer = document.getElementById("answer");
            if (answer) {
                answer.readOnly = true;
            }
            // Yükleniyor göstergesini aç
            var loader = document.getElementById("loadingIndicator");
            if (loader) {
                loader.classList.remove("d-none");
            }
        });
    }

    // 3. Değerlendirme Raporunu İndirme Fonksiyonları
    const downloadPdfBtn = document.getElementById("downloadPdfBtn");
    const downloadTxtBtn = document.getElementById("downloadTxtBtn");

    if (downloadPdfBtn) {
        downloadPdfBtn.addEventListener("click", function () {
            const cardElement = document.getElementById("evaluationCard");

            // Yazdırma işlemi için temiz bir pencere açıyoruz
            const printWindow = window.open('', '_blank', 'width=900,height=800');
            if (!printWindow) {
                alert("Tarayıcınız pencere açılmasını engelledi. Lütfen pop-up engelleyicisini kapatıp tekrar deneyin.");
                return;
            }

            // Pencere içeriğini oluşturuyoruz
            printWindow.document.write(
                '<!DOCTYPE html>' +
                '<html data-bs-theme="light">' +
                '<head>' +
                '    <title>' + jobTitle + ' - İK Değerlendirme Raporu</title>' +
                '    <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css" rel="stylesheet">' +
                '    <link rel="stylesheet" href="https://cdn.jsdelivr.net/npm/bootstrap-icons@1.11.3/font/bootstrap-icons.min.css">' +
                '    <style>' +
                '        body {' +
                '            padding: 30px;' +
                '            background-color: #ffffff;' +
                '            font-family: \'Segoe UI\', Roboto, Helvetica, Arial, sans-serif;' +
                '        }' +
                '        .card {' +
                '            border: 1px solid #dee2e6 !important;' +
                '            box-shadow: none !important;' +
                '        }' +
                '        [data-html2canvas-ignore="true"] {' +
                '            display: none !important;' +
                '        }' +
                '        @media print {' +
                '            body {' +
                '                padding: 0;' +
                '            }' +
                '            .card {' +
                '                border: none !important;' +
                '            }' +
                '            * {' +
                '                -webkit-print-color-adjust: exact !important;' +
                '                print-color-adjust: exact !important;' +
                '            }' +
                '        }' +
                '    </style>' +
                '</head>' +
                '<body>' +
                '    <div class="container">' +
                cardElement.outerHTML +
                '    </div>' +
                '    <script>' +
                '        window.onload = function() {' +
                '            setTimeout(function() {' +
                '                window.print();' +
                '                window.close();' +
                '            }, 400);' +
                '        };' +
                '    <\/script>' +
                '</body>' +
                '</html>'
            );
            printWindow.document.close();
        });
    }

    if (downloadTxtBtn) {
        downloadTxtBtn.addEventListener("click", function () {
            const rawDiv = document.getElementById("raw-evaluation");
            if (rawDiv) {
                const markdownText = rawDiv.textContent;
                const filename = `${jobTitle.replace(/[^a-zA-Z0-9]/g, "_")}_IK_Degerlendirme.txt`;

                const blob = new Blob([markdownText], { type: "text/plain;charset=utf-8" });
                const link = document.createElement("a");
                link.href = URL.createObjectURL(blob);
                link.download = filename;

                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                URL.revokeObjectURL(link.href);
            }
        });
    }

    // 4. Hazırlık modunda ipucu alma
    const hintBtn = document.getElementById("hintBtn");
    const hintBox = document.getElementById("hintBox");
    if (hintBtn && hintBox) {
        hintBtn.addEventListener("click", function () {
            hintBtn.disabled = true;
            hintBox.classList.remove("d-none");
            hintBox.textContent = "İpucu hazırlanıyor...";

            const formData = new URLSearchParams();
            formData.append("id", sessionId);

            fetch("/Interview/GetHint", {
                method: "POST",
                headers: { "Content-Type": "application/x-www-form-urlencoded" },
                body: formData.toString()
            })
                .then(function (response) {
                    if (!response.ok) {
                        throw new Error("İpucu alınamadı.");
                    }
                    return response.json();
                })
                .then(function (data) {
                    hintBox.textContent = data.hint;
                })
                .catch(function () {
                    hintBox.textContent = "İpucu şu anda alınamadı, lütfen tekrar deneyin.";
                })
                .finally(function () {
                    hintBtn.disabled = false;
                });
        });
    }
});
