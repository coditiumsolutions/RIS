(() => {
  const MAX_EDGE = 1280;
  const JPEG_QUALITY = 0.8;

  const stopStream = (stream) => {
    if (!stream) {
      return;
    }
    stream.getTracks().forEach((track) => track.stop());
  };

  const compressToJpeg = (sourceCanvas) =>
    new Promise((resolve, reject) => {
      const width = sourceCanvas.width;
      const height = sourceCanvas.height;
      const scale = Math.min(1, MAX_EDGE / Math.max(width, height));
      const target = document.createElement("canvas");
      target.width = Math.max(1, Math.round(width * scale));
      target.height = Math.max(1, Math.round(height * scale));
      const ctx = target.getContext("2d");
      if (!ctx) {
        reject(new Error("Canvas unavailable."));
        return;
      }
      ctx.drawImage(sourceCanvas, 0, 0, target.width, target.height);
      target.toBlob(
        (blob) => {
          if (!blob) {
            reject(new Error("Could not encode photo."));
            return;
          }
          resolve(blob);
        },
        "image/jpeg",
        JPEG_QUALITY
      );
    });

  const captureOptional = async (kindLabel) => {
    if (typeof Swal === "undefined" || typeof Swal.fire !== "function") {
      return { skipped: true };
    }

    if (!navigator.mediaDevices || typeof navigator.mediaDevices.getUserMedia !== "function") {
      await Swal.fire({
        icon: "info",
        title: "Camera unavailable",
        text: "This device cannot open a camera. Continuing without a photo.",
        confirmButtonText: "Continue"
      });
      return { skipped: true };
    }

    const intro = await Swal.fire({
      title: "Take a photo?",
      text: `Optionally capture a ${kindLabel || "visitor"} photo. You can skip this step.`,
      icon: "question",
      showDenyButton: true,
      showCancelButton: true,
      focusCancel: true,
      reverseButtons: true,
      confirmButtonText: "Take photo",
      denyButtonText: "Skip",
      cancelButtonText: "Cancel"
    });

    if (intro.isDenied) {
      return { skipped: true };
    }
    if (!intro.isConfirmed) {
      return { cancelled: true };
    }

    let stream = null;
    let devices = [];
    let deviceIndex = 0;
    let previewUrl = null;
    let capturedBlob = null;

    const cleanup = () => {
      stopStream(stream);
      stream = null;
      if (previewUrl) {
        URL.revokeObjectURL(previewUrl);
        previewUrl = null;
      }
    };

    const startCamera = async (deviceId) => {
      stopStream(stream);
      stream = null;
      const constraints = {
        audio: false,
        video: deviceId
          ? { deviceId: { exact: deviceId } }
          : { facingMode: { ideal: "environment" }, width: { ideal: 1280 }, height: { ideal: 720 } }
      };
      stream = await navigator.mediaDevices.getUserMedia(constraints);
      return stream;
    };

    try {
      stream = await startCamera(null);
      try {
        const all = await navigator.mediaDevices.enumerateDevices();
        devices = all.filter((d) => d.kind === "videoinput");
      } catch {
        devices = [];
      }
    } catch {
      cleanup();
      await Swal.fire({
        icon: "info",
        title: "Camera access denied",
        text: "Could not open the camera. Continuing without a photo.",
        confirmButtonText: "Continue"
      });
      return { skipped: true };
    }

    const result = await Swal.fire({
      title: "Camera",
      html: `
        <div class="visitor-camera-panel">
          <video class="visitor-camera-video" playsinline muted autoplay></video>
          <img class="visitor-camera-preview d-none" alt="Captured preview" />
          <p class="visitor-camera-status text-secondary small mb-0"></p>
          <div class="visitor-camera-actions">
            <button type="button" class="btn btn-sm btn-outline-secondary" data-cam-switch>Switch camera</button>
            <button type="button" class="btn btn-sm btn-primary" data-cam-snap>Snap</button>
            <button type="button" class="btn btn-sm btn-outline-secondary d-none" data-cam-retake>Retake</button>
          </div>
        </div>
      `,
      showCancelButton: true,
      showDenyButton: true,
      confirmButtonText: "Use photo",
      denyButtonText: "Skip",
      cancelButtonText: "Cancel",
      reverseButtons: true,
      focusConfirm: false,
      allowOutsideClick: false,
      preConfirm: () => {
        if (!capturedBlob) {
          Swal.showValidationMessage("Snap a photo first, or tap Skip.");
          return false;
        }
        return capturedBlob;
      },
      didOpen: () => {
        const popup = Swal.getPopup();
        if (!popup) {
          return;
        }
        const video = popup.querySelector(".visitor-camera-video");
        const preview = popup.querySelector(".visitor-camera-preview");
        const status = popup.querySelector(".visitor-camera-status");
        const snapBtn = popup.querySelector("[data-cam-snap]");
        const retakeBtn = popup.querySelector("[data-cam-retake]");
        const switchBtn = popup.querySelector("[data-cam-switch]");
        const confirmBtn = Swal.getConfirmButton();

        if (confirmBtn) {
          confirmBtn.disabled = true;
        }

        if (video && stream) {
          video.srcObject = stream;
          video.play().catch(() => {});
        }

        if (switchBtn) {
          if (devices.length < 2) {
            switchBtn.classList.add("d-none");
          }
          switchBtn.addEventListener("click", async () => {
            if (devices.length < 2) {
              return;
            }
            deviceIndex = (deviceIndex + 1) % devices.length;
            try {
              stream = await startCamera(devices[deviceIndex].deviceId);
              if (video) {
                video.srcObject = stream;
                await video.play().catch(() => {});
              }
              if (status) {
                status.textContent = "";
              }
            } catch {
              if (status) {
                status.textContent = "Could not switch camera.";
              }
            }
          });
        }

        const showLive = () => {
          capturedBlob = null;
          if (previewUrl) {
            URL.revokeObjectURL(previewUrl);
            previewUrl = null;
          }
          if (preview) {
            preview.classList.add("d-none");
            preview.removeAttribute("src");
          }
          if (video) {
            video.classList.remove("d-none");
          }
          snapBtn?.classList.remove("d-none");
          retakeBtn?.classList.add("d-none");
          if (confirmBtn) {
            confirmBtn.disabled = true;
          }
        };

        snapBtn?.addEventListener("click", async () => {
          if (!video || !stream) {
            return;
          }
          const canvas = document.createElement("canvas");
          canvas.width = video.videoWidth || 1280;
          canvas.height = video.videoHeight || 720;
          const ctx = canvas.getContext("2d");
          if (!ctx) {
            return;
          }
          ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
          try {
            capturedBlob = await compressToJpeg(canvas);
            if (previewUrl) {
              URL.revokeObjectURL(previewUrl);
            }
            previewUrl = URL.createObjectURL(capturedBlob);
            if (preview) {
              preview.src = previewUrl;
              preview.classList.remove("d-none");
            }
            video.classList.add("d-none");
            snapBtn.classList.add("d-none");
            retakeBtn?.classList.remove("d-none");
            if (confirmBtn) {
              confirmBtn.disabled = false;
            }
            if (status) {
              status.textContent = "Photo ready. Use photo, retake, or skip.";
            }
          } catch {
            if (status) {
              status.textContent = "Could not capture photo. Try again or skip.";
            }
          }
        });

        retakeBtn?.addEventListener("click", showLive);
      },
      willClose: cleanup
    });

    cleanup();

    if (result.isDenied) {
      return { skipped: true };
    }
    if (!result.isConfirmed) {
      return { cancelled: true };
    }
    if (!(result.value instanceof Blob)) {
      return { skipped: true };
    }
    return { blob: result.value };
  };

  const attachPhotoToForm = (form, blob) => {
    if (!(form instanceof HTMLFormElement)) {
      return;
    }

    form.setAttribute("enctype", "multipart/form-data");
    let input = form.querySelector('input[name="photo"]');
    if (!(input instanceof HTMLInputElement)) {
      input = document.createElement("input");
      input.type = "file";
      input.name = "photo";
      input.accept = "image/jpeg,image/png";
      input.hidden = true;
      form.appendChild(input);
    }

    if (!blob) {
      input.value = "";
      return;
    }

    const file = new File([blob], "capture.jpg", { type: "image/jpeg" });
    const transfer = new DataTransfer();
    transfer.items.add(file);
    input.files = transfer.files;
  };

  const openPhotoLightbox = (url, title) => {
    if (typeof Swal === "undefined" || typeof Swal.fire !== "function") {
      window.open(url, "_blank", "noopener");
      return;
    }
    Swal.fire({
      title: title || "Visitor photo",
      imageUrl: url,
      imageAlt: title || "Visitor photo",
      showConfirmButton: true,
      confirmButtonText: "Close",
      width: "min(96vw, 720px)"
    });
  };

  document.addEventListener("click", (event) => {
    const trigger = event.target instanceof Element
      ? event.target.closest("[data-visitor-photo-open]")
      : null;
    if (!(trigger instanceof HTMLElement)) {
      return;
    }
    event.preventDefault();
    const url = trigger.getAttribute("href") || trigger.getAttribute("data-visitor-photo-open");
    if (!url) {
      return;
    }
    openPhotoLightbox(url, trigger.getAttribute("data-visitor-photo-title") || "Visitor photo");
  });

  window.VisitorCamera = {
    captureOptional,
    attachPhotoToForm
  };
})();
