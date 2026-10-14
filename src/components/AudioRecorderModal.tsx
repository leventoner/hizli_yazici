import React, { useState, useEffect, useRef } from 'react';
import { Mic, Square, Loader2, Sparkles, Volume2, Check } from 'lucide-react';

interface AudioRecorderModalProps {
  isOpen: boolean;
  durationSeconds: number;
  themeColor: string;
  onClose: () => void;
  onTranscriptionComplete: (text: string) => void;
  onError: (errorMsg: string) => void;
}

export const AudioRecorderModal: React.FC<AudioRecorderModalProps> = ({
  isOpen,
  durationSeconds,
  themeColor,
  onClose,
  onTranscriptionComplete,
  onError,
}) => {
  const [isRecording, setIsRecording] = useState(false);
  const [secondsLeft, setSecondsLeft] = useState(durationSeconds);
  const [isProcessing, setIsProcessing] = useState(false);
  const [interimText, setInterimText] = useState('');
  const [audioLevel, setAudioLevel] = useState(0);
  const [detectedEngine, setDetectedEngine] = useState<string>('Web Speech + Gemini AI');

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const timerRef = useRef<any>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const analyserRef = useRef<AnalyserNode | null>(null);
  const animFrameRef = useRef<number | null>(null);
  const speechRecognitionRef = useRef<any>(null);
  const finalTranscriptRef = useRef<string>('');
  const recordedMimeTypeRef = useRef<string>('audio/webm');

  useEffect(() => {
    if (isOpen) {
      setSecondsLeft(durationSeconds);
      setInterimText('');
      finalTranscriptRef.current = '';
      startRecording();
    } else {
      stopRecordingCleanup();
    }

    return () => {
      stopRecordingCleanup();
    };
  }, [isOpen]);

  const stopRecordingCleanup = () => {
    if (timerRef.current) clearInterval(timerRef.current);
    if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
    if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
      try {
        mediaRecorderRef.current.stop();
      } catch {}
    }
    if (speechRecognitionRef.current) {
      try {
        speechRecognitionRef.current.stop();
      } catch {}
    }
    if (audioContextRef.current && audioContextRef.current.state !== 'closed') {
      try {
        audioContextRef.current.close();
      } catch {}
    }
    setIsRecording(false);
  };

  const startRecording = async () => {
    try {
      audioChunksRef.current = [];
      finalTranscriptRef.current = '';
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });

      // Audio analysis for waveform animation
      try {
        const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
        audioContextRef.current = audioCtx;
        const analyser = audioCtx.createAnalyser();
        analyserRef.current = analyser;
        analyser.fftSize = 64;
        const source = audioCtx.createMediaStreamSource(stream);
        source.connect(analyser);

        const dataArray = new Uint8Array(analyser.frequencyBinCount);
        const updateLevel = () => {
          if (!analyserRef.current) return;
          analyserRef.current.getByteFrequencyData(dataArray);
          let sum = 0;
          for (let i = 0; i < dataArray.length; i++) {
            sum += dataArray[i];
          }
          const avg = sum / dataArray.length;
          setAudioLevel(Math.min(100, Math.round((avg / 128) * 100)));
          animFrameRef.current = requestAnimationFrame(updateLevel);
        };
        updateLevel();
      } catch (err) {
        console.warn('AudioContext visualization not available:', err);
      }

      // Check for Web Speech Recognition for live real-time preview (Turkish)
      const SpeechRecognition =
        (window as any).SpeechRecognition || (window as any).webkitSpeechRecognition;

      if (SpeechRecognition) {
        try {
          const recognition = new SpeechRecognition();
          recognition.lang = 'tr-TR';
          recognition.continuous = true;
          recognition.interimResults = true;

          recognition.onresult = (event: any) => {
            let finalChunk = '';
            let currentInterim = '';

            for (let i = 0; i < event.results.length; ++i) {
              const res = event.results[i];
              if (res.isFinal) {
                finalChunk += res[0].transcript + ' ';
              } else {
                currentInterim += res[0].transcript;
              }
            }

            finalTranscriptRef.current = finalChunk.trim();
            const total = (finalChunk + currentInterim).trim();
            if (total) {
              setInterimText(total);
            }
          };

          recognition.onerror = (e: any) => {
            console.warn('SpeechRecognition error:', e);
          };

          recognition.start();
          speechRecognitionRef.current = recognition;
          setDetectedEngine('Canlı Web Speech (tr-TR) + Gemini');
        } catch (e) {
          console.warn('SpeechRecognition failed to start:', e);
          setDetectedEngine('Gemini AI Multimodal');
        }
      } else {
        setDetectedEngine('Gemini AI Multimodal');
      }

      // Determine supported mimeType safely
      let mimeType = 'audio/webm';
      if (typeof MediaRecorder !== 'undefined') {
        if (MediaRecorder.isTypeSupported('audio/webm;codecs=opus')) {
          mimeType = 'audio/webm;codecs=opus';
        } else if (MediaRecorder.isTypeSupported('audio/webm')) {
          mimeType = 'audio/webm';
        } else if (MediaRecorder.isTypeSupported('audio/mp4')) {
          mimeType = 'audio/mp4';
        } else if (MediaRecorder.isTypeSupported('audio/ogg')) {
          mimeType = 'audio/ogg';
        } else {
          mimeType = '';
        }
      }

      recordedMimeTypeRef.current = mimeType || 'audio/webm';

      const recorder = mimeType ? new MediaRecorder(stream, { mimeType }) : new MediaRecorder(stream);
      mediaRecorderRef.current = recorder;

      recorder.ondataavailable = (event) => {
        if (event.data && event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      recorder.onstop = async () => {
        stream.getTracks().forEach((track) => track.stop());
        await processAudioPayload();
      };

      recorder.start(500);
      setIsRecording(true);

      // Countdown timer
      let remaining = durationSeconds;
      timerRef.current = setInterval(() => {
        remaining -= 1;
        setSecondsLeft(remaining);
        if (remaining <= 0) {
          clearInterval(timerRef.current);
          finishRecording();
        }
      }, 1000);
    } catch (err: any) {
      console.error('Microphone error:', err);
      onError('Mikrofona erişilemedi. Lütfen tarayıcı izinlerini kontrol edin.');
      onClose();
    }
  };

  const finishRecording = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
      try {
        mediaRecorderRef.current.stop();
      } catch {}
    }
    if (speechRecognitionRef.current) {
      try {
        speechRecognitionRef.current.stop();
      } catch {}
    }
    setIsRecording(false);
    if (timerRef.current) clearInterval(timerRef.current);
  };

  const processAudioPayload = async () => {
    setIsProcessing(true);
    const liveCapturedText = (finalTranscriptRef.current || interimText || '').trim();

    try {
      const audioBlob = new Blob(audioChunksRef.current, {
        type: recordedMimeTypeRef.current || 'audio/webm',
      });

      // Convert blob to base64
      const reader = new FileReader();
      const base64Promise = new Promise<string>((resolve, reject) => {
        reader.onloadend = () => {
          const res = reader.result as string;
          resolve(res);
        };
        reader.onerror = reject;
      });
      reader.readAsDataURL(audioBlob);
      const base64Audio = await base64Promise;

      // Send to server Gemini STT with safe clean MIME
      const cleanMime = (recordedMimeTypeRef.current || 'audio/webm').split(';')[0].trim();
      const res = await fetch('/api/transcribe', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          audioData: base64Audio,
          mimeType: cleanMime,
        }),
      });

      if (res.ok) {
        const data = await res.json();
        const serverTranscription = (data.transcription || '').trim();
        if (serverTranscription) {
          onTranscriptionComplete(serverTranscription);
          onClose();
          return;
        }
      }

      // If server returned error or empty, but Web Speech captured text:
      if (liveCapturedText) {
        console.log('Gemini yanıt vermedi, Web Speech canlı transkripsiyonu aktarılıyor:', liveCapturedText);
        onTranscriptionComplete(liveCapturedText);
        onClose();
        return;
      }

      throw new Error('Ses algılanamadı veya metne çevrilemedi.');
    } catch (err: any) {
      console.error('Transcription error:', err);
      if (liveCapturedText) {
        onTranscriptionComplete(liveCapturedText);
        onClose();
      } else {
        onError(err.message || 'Ses işleme sırasında bir hata meydana geldi.');
        onClose();
      }
    } finally {
      setIsProcessing(false);
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/80 backdrop-blur-md animate-in fade-in">
      <div className="relative w-full max-w-md bg-slate-900 border border-slate-700/80 rounded-3xl p-6 shadow-2xl flex flex-col items-center text-center overflow-hidden">
        {/* Glow ambient background */}
        <div
          className="absolute -top-24 -left-24 w-48 h-48 rounded-full blur-3xl opacity-20 pointer-events-none transition-all duration-500"
          style={{ backgroundColor: themeColor, transform: `scale(${1 + audioLevel / 50})` }}
        />
        <div
          className="absolute -bottom-24 -right-24 w-48 h-48 rounded-full blur-3xl opacity-20 pointer-events-none transition-all duration-500"
          style={{ backgroundColor: themeColor, transform: `scale(${1 + audioLevel / 50})` }}
        />

        {/* Status Header */}
        <div className="flex items-center gap-2 mb-4">
          <span className="w-2.5 h-2.5 rounded-full bg-rose-500 animate-pulse" />
          <span className="text-xs font-semibold tracking-wider uppercase text-slate-400">
            {isProcessing ? 'Ses Metne Dönüştürülüyor...' : 'Ses Kaydediliyor'}
          </span>
        </div>

        <div className="mb-4">
          <span className="text-[10px] font-medium px-2 py-0.5 rounded-full bg-slate-800 text-slate-400 border border-slate-700 inline-flex items-center gap-1">
            <Volume2 className="w-3 h-3 text-emerald-400" />
            {detectedEngine}
          </span>
        </div>

        {/* Central Pulse Microphone */}
        <div className="relative my-2 flex items-center justify-center">
          {isRecording && (
            <>
              <div
                className="absolute w-36 h-36 rounded-full border border-rose-500/30 animate-ping opacity-60"
                style={{ animationDuration: '2s' }}
              />
              <div
                className="absolute w-28 h-28 rounded-full opacity-40 transition-transform duration-75"
                style={{
                  backgroundColor: themeColor,
                  transform: `scale(${1 + audioLevel / 80})`,
                }}
              />
            </>
          )}

          <div
            className="relative z-10 w-24 h-24 rounded-full flex items-center justify-center shadow-xl transition-all"
            style={{
              backgroundColor: isProcessing ? '#475569' : '#dc2626',
            }}
          >
            {isProcessing ? (
              <Loader2 className="w-10 h-10 text-white animate-spin" />
            ) : (
              <Mic className="w-10 h-10 text-white" />
            )}
          </div>
        </div>

        {/* Live Audio Visualizer Bars */}
        <div className="flex items-center justify-center gap-1.5 h-8 my-3">
          {[40, 70, 90, 60, 30, 80, 100, 70, 50, 85, 65, 40].map((h, i) => {
            const dynamicHeight = isRecording ? Math.max(6, (h * (audioLevel + 20)) / 100) : 6;
            return (
              <div
                key={i}
                className="w-1.5 rounded-full transition-all duration-75"
                style={{
                  height: `${dynamicHeight}px`,
                  backgroundColor: isRecording ? themeColor : '#475569',
                }}
              />
            );
          })}
        </div>

        {/* Timer */}
        <div className="text-3xl font-mono font-bold text-white tracking-wider mb-1">
          00:{secondsLeft < 10 ? `0${secondsLeft}` : secondsLeft}
        </div>
        <p className="text-xs text-slate-400 max-w-xs mb-3">
          Konuşun; sesiniz canlı olarak algılanır ve kaydı bitirdiğinizde tam metin olarak aktarılır.
        </p>

        {/* Live Transcript Preview */}
        {interimText ? (
          <div className="w-full bg-slate-800/90 border border-emerald-500/40 rounded-xl p-3 text-xs text-slate-200 font-sans text-left mb-4 max-h-24 overflow-y-auto">
            <span className="text-[10px] uppercase font-bold text-emerald-400 flex items-center gap-1 mb-1">
              <Check className="w-3 h-3" /> Canlı Algılanan Metin:
            </span>
            &ldquo;{interimText}&rdquo;
          </div>
        ) : (
          <div className="w-full bg-slate-800/40 border border-slate-700/40 rounded-xl p-2.5 text-xs text-slate-500 text-center mb-4">
            Konuşmaya başladığınızda canlı metin burada belirecektir...
          </div>
        )}

        {/* Control Buttons */}
        <div className="flex items-center gap-3 w-full mt-1">
          <button
            onClick={() => {
              stopRecordingCleanup();
              onClose();
            }}
            disabled={isProcessing}
            className="flex-1 py-2.5 px-4 rounded-xl bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white text-sm font-medium transition-colors"
          >
            İptal
          </button>
          <button
            onClick={finishRecording}
            disabled={isProcessing}
            className="flex-1 py-2.5 px-4 rounded-xl bg-rose-600 hover:bg-rose-500 text-white text-sm font-semibold flex items-center justify-center gap-2 shadow-lg shadow-rose-900/30 transition-transform active:scale-95 disabled:opacity-50"
          >
            {isProcessing ? (
              <>
                <Loader2 className="w-4 h-4 animate-spin" />
                İşleniyor
              </>
            ) : (
              <>
                <Square className="w-4 h-4 fill-white" />
                Kaydı Bitir
              </>
            )}
          </button>
        </div>
      </div>
    </div>
  );
};
