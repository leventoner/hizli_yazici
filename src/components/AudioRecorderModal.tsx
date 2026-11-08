import React, { useState, useEffect, useRef } from 'react';
import { Mic, Square, Loader2, Sparkles, Volume2, Check, X, Copy } from 'lucide-react';

interface AudioRecorderModalProps {
  isOpen: boolean;
  durationSeconds?: number;
  themeColor: string;
  onClose: () => void;
  onTranscriptionComplete: (text: string) => void;
  onError: (errorMsg: string) => void;
}

export const AudioRecorderModal: React.FC<AudioRecorderModalProps> = ({
  isOpen,
  themeColor,
  onClose,
  onTranscriptionComplete,
  onError,
}) => {
  const [isRecording, setIsRecording] = useState(false);
  const [elapsedSeconds, setElapsedSeconds] = useState(0);
  const [isProcessing, setIsProcessing] = useState(false);
  const [interimText, setInterimText] = useState('');
  const [transcribedResult, setTranscribedResult] = useState('');
  const [frequencies, setFrequencies] = useState<number[]>(new Array(20).fill(6));
  const [copied, setCopied] = useState(false);

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const timerRef = useRef<any>(null);
  const audioContextRef = useRef<AudioContext | null>(null);
  const analyserRef = useRef<AnalyserNode | null>(null);
  const animFrameRef = useRef<number | null>(null);
  const speechRecognitionRef = useRef<any>(null);
  const finalTranscriptRef = useRef<string>('');
  const recordedMimeTypeRef = useRef<string>('audio/webm');
  const streamRef = useRef<MediaStream | null>(null);

  // Bar weights for Gaussian distribution waveform
  const barWeights = [
    0.25, 0.35, 0.5, 0.65, 0.8, 0.92, 1.0, 1.15, 1.25, 1.3,
    1.3, 1.25, 1.15, 1.0, 0.92, 0.8, 0.65, 0.5, 0.35, 0.25
  ];

  useEffect(() => {
    if (isOpen) {
      setElapsedSeconds(0);
      setInterimText('');
      setTranscribedResult('');
      finalTranscriptRef.current = '';
      setFrequencies(new Array(20).fill(6));
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

    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop());
      streamRef.current = null;
    }

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
      setInterimText('');
      setTranscribedResult('');
      setElapsedSeconds(0);

      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;

      // Audio analysis for frequency waveform animation
      try {
        const audioCtx = new (window.AudioContext || (window as any).webkitAudioContext)();
        audioContextRef.current = audioCtx;
        const analyser = audioCtx.createAnalyser();
        analyserRef.current = analyser;
        analyser.fftSize = 64;
        const source = audioCtx.createMediaStreamSource(stream);
        source.connect(analyser);

        const dataArray = new Uint8Array(analyser.frequencyBinCount);
        const updateVisualizer = () => {
          if (!analyserRef.current) return;
          analyserRef.current.getByteFrequencyData(dataArray);

          // Calculate average sound level
          let sum = 0;
          for (let i = 0; i < dataArray.length; i++) {
            sum += dataArray[i];
          }
          const avgLevel = sum / (dataArray.length * 128); // 0.0 to 1.0

          // Map to 20 waveform bars
          const nextBars = barWeights.map((w) => {
            const jitter = 0.85 + Math.random() * 0.3;
            const h = Math.round(6 + avgLevel * 34 * w * jitter);
            return Math.min(38, Math.max(6, h));
          });
          setFrequencies(nextBars);

          animFrameRef.current = requestAnimationFrame(updateVisualizer);
        };
        updateVisualizer();
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
        } catch (e) {
          console.warn('SpeechRecognition failed to start:', e);
        }
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
        if (streamRef.current) {
          streamRef.current.getTracks().forEach((track) => track.stop());
          streamRef.current = null;
        }
        await processAudioPayload();
      };

      recorder.start(500);
      setIsRecording(true);

      // Increment elapsed timer (No 10s auto-stop!)
      timerRef.current = setInterval(() => {
        setElapsedSeconds((prev) => prev + 1);
      }, 1000);
    } catch (err: any) {
      console.error('Microphone error:', err);
      onError('Mikrofona erişilemedi. Lütfen tarayıcı mikrofon izinlerini kontrol edin.');
    }
  };

  const handleStopRecording = () => {
    if (timerRef.current) clearInterval(timerRef.current);
    if (animFrameRef.current) cancelAnimationFrame(animFrameRef.current);
    setFrequencies(new Array(20).fill(6));

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

      let finalTranscribed = '';

      if (res.ok) {
        const data = await res.json();
        const serverTranscription = (data.transcription || '').trim();
        if (serverTranscription) {
          finalTranscribed = serverTranscription;
        }
      }

      if (!finalTranscribed && liveCapturedText) {
        finalTranscribed = liveCapturedText;
      }

      if (finalTranscribed) {
        setTranscribedResult(finalTranscribed);
        // Automatically copy to clipboard!
        navigator.clipboard?.writeText(finalTranscribed).catch(() => {});
        onTranscriptionComplete(finalTranscribed);
      } else {
        throw new Error('Ses algılanamadı veya metne çevrilemedi.');
      }
    } catch (err: any) {
      console.error('Transcription error:', err);
      if (liveCapturedText) {
        setTranscribedResult(liveCapturedText);
        navigator.clipboard?.writeText(liveCapturedText).catch(() => {});
        onTranscriptionComplete(liveCapturedText);
      } else {
        onError(err.message || 'Ses işleme sırasında bir hata meydana geldi.');
      }
    } finally {
      setIsProcessing(false);
    }
  };

  const handleCopyResult = () => {
    if (transcribedResult) {
      navigator.clipboard?.writeText(transcribedResult);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  if (!isOpen) return null;

  const minutes = Math.floor(elapsedSeconds / 60);
  const seconds = elapsedSeconds % 60;
  const timeFormatted = `${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/75 backdrop-blur-md animate-in fade-in">
      <div className="relative w-full max-w-md bg-gradient-to-b from-slate-900 via-slate-950 to-slate-950 border border-slate-800 rounded-3xl p-6 shadow-2xl flex flex-col items-center text-center overflow-hidden">
        
        {/* Glow ambient background */}
        <div
          className="absolute -top-20 -left-20 w-44 h-44 rounded-full blur-3xl opacity-20 pointer-events-none transition-all duration-300"
          style={{ backgroundColor: isRecording ? '#10b981' : themeColor }}
        />
        <div
          className="absolute -bottom-20 -right-20 w-44 h-44 rounded-full blur-3xl opacity-20 pointer-events-none transition-all duration-300"
          style={{ backgroundColor: isRecording ? '#06b6d4' : themeColor }}
        />

        {/* Top Header */}
        <div className="w-full flex items-center justify-between pb-3 border-b border-slate-800/80 mb-4">
          <div className="flex items-center gap-2">
            <span className="p-1.5 rounded-lg bg-slate-800 text-sm">🎙️</span>
            <div className="text-left">
              <h3 className="text-sm font-bold text-white leading-tight">Sesle Yaz</h3>
              <p className="text-[10px] text-slate-400">Yüzen Ses Kaydı &amp; Çevirici</p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => {
              stopRecordingCleanup();
              onClose();
            }}
            className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition-colors"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Status Indicator */}
        <div className="flex items-center gap-2 mb-2">
          <span
            className={`w-2.5 h-2.5 rounded-full ${
              isRecording
                ? 'bg-rose-500 animate-pulse'
                : isProcessing
                ? 'bg-blue-400 animate-spin'
                : 'bg-emerald-400'
            }`}
          />
          <span className="text-xs font-semibold tracking-wider text-slate-300">
            {isProcessing
              ? 'Metne Çevriliyor... (Lokal AI)'
              : isRecording
              ? 'Kaydediliyor... Konuşun'
              : transcribedResult
              ? 'Tamamlandı (Panoya Kopyalandı)'
              : "Kayıt için 'Başlat' düğmesine basın"}
          </span>
        </div>

        {/* Timer Display */}
        <div className="text-3xl font-mono font-bold text-white tracking-wider my-2">
          {timeFormatted}
        </div>

        {/* 20 Frequency Waveform Bars */}
        <div className="w-full bg-slate-950/70 border border-slate-800/90 rounded-2xl py-3 px-4 my-2 flex items-center justify-center gap-1.5 h-14">
          {frequencies.map((h, i) => (
            <div
              key={i}
              className="w-1.5 rounded-full transition-all duration-75"
              style={{
                height: `${h}px`,
                background: isRecording
                  ? 'linear-gradient(180deg, #06b6d4 0%, #10b981 100%)'
                  : '#1e293b',
              }}
            />
          ))}
        </div>

        {/* Live Interim Speech Preview during recording */}
        {isRecording && interimText && (
          <div className="w-full bg-slate-900/80 border border-emerald-500/30 rounded-xl p-3 text-xs text-slate-200 text-left my-2 max-h-20 overflow-y-auto">
            <span className="text-[10px] uppercase font-bold text-emerald-400 flex items-center gap-1 mb-1">
              <Check className="w-3 h-3" /> Canlı Algılanan:
            </span>
            &ldquo;{interimText}&rdquo;
          </div>
        )}

        {/* Transcribed Result Area */}
        {transcribedResult && (
          <div className="w-full bg-blue-950/30 border border-blue-500/40 rounded-xl p-3 text-xs text-slate-100 text-left my-2">
            <div className="flex items-center justify-between mb-1.5">
              <span className="text-[10px] uppercase font-bold text-emerald-400 flex items-center gap-1">
                <Check className="w-3 h-3" /> Metne Çevrildi &amp; Kopyalandı:
              </span>
              <button
                type="button"
                onClick={handleCopyResult}
                className="text-[11px] font-medium text-sky-400 hover:text-sky-300 flex items-center gap-1"
              >
                {copied ? <Check className="w-3 h-3 text-emerald-400" /> : <Copy className="w-3 h-3" />}
                {copied ? 'Kopyalandı' : 'Kopyala'}
              </button>
            </div>
            <p className="font-mono text-slate-200">{transcribedResult}</p>
          </div>
        )}

        {/* Main Controls: Start or Stop */}
        <div className="w-full mt-4 flex items-center gap-3">
          {!isRecording ? (
            <button
              type="button"
              onClick={startRecording}
              disabled={isProcessing}
              className="flex-1 py-3 px-6 rounded-2xl bg-gradient-to-r from-emerald-600 to-teal-500 hover:from-emerald-500 hover:to-teal-400 text-white font-semibold text-sm flex items-center justify-center gap-2 shadow-lg shadow-emerald-900/40 active:scale-95 transition-all disabled:opacity-50"
            >
              {isProcessing ? (
                <>
                  <Loader2 className="w-4 h-4 animate-spin" />
                  İşleniyor...
                </>
              ) : (
                <>
                  <Mic className="w-4 h-4" />
                  Kaydı Başlat
                </>
              )}
            </button>
          ) : (
            <button
              type="button"
              onClick={handleStopRecording}
              disabled={isProcessing}
              className="flex-1 py-3 px-6 rounded-2xl bg-gradient-to-r from-rose-600 to-red-500 hover:from-rose-500 hover:to-red-400 text-white font-semibold text-sm flex items-center justify-center gap-2 shadow-lg shadow-red-900/50 active:scale-95 transition-all"
            >
              <Square className="w-4 h-4 fill-white" />
              Durdur &amp; Metne Çevir
            </button>
          )}

          <button
            type="button"
            onClick={() => {
              stopRecordingCleanup();
              onClose();
            }}
            className="py-3 px-5 rounded-2xl bg-slate-800 hover:bg-slate-700 text-slate-300 hover:text-white text-sm font-medium transition-colors"
          >
            Kapat
          </button>
        </div>

        {/* Subtle Footer Note */}
        <p className="text-[10px] text-slate-500 mt-3">
          Kayıt durdurulduğunda sonuç doğrudan panoya kopyalanır.
        </p>
      </div>
    </div>
  );
};
