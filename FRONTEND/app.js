// Globális változók
const dayCapacity = 600; // nap kapacitása percekben
const breakTime = 30;    // pihenő idő percekben
const taskColors = {};

// Letölti az ütemterv adatokat a szerverről és rendereli
async function downloadAndDisplay() {
  try {
    const response = await fetch('http://localhost:5267/scheduler');
    const sched = await response.json();
    renderSchedule(sched);
  } catch (error) {
    console.error('Error fetching schedule:', error);
  }
}

// Táblázat generálása: 2 + 600 oszlop, a feladatok balról jobbra töltenek be colspannel
function renderSchedule(days) {
  const container = document.getElementById('scheduleContainer');
  container.innerHTML = '';

  // Létrehozzuk a fő táblázatot
  const table = document.createElement('table');
  table.classList.add('table', 'table-borderless','table-sm');

  // colgroup: 2 keskeny + 600 oszlop
  const colgroup = document.createElement('colgroup');
  // 1. oszlop: Day #
  const colDay = document.createElement('col');
  colgroup.appendChild(colDay);

  // 2. oszlop: Effective Load
  const colLoad = document.createElement('col');
  colgroup.appendChild(colLoad);

  // 600 oszlop a perceknek
  for (let i = 0; i < dayCapacity; i++) {
    const col = document.createElement('col');
    colgroup.appendChild(col);
  }
  table.appendChild(colgroup);

  // Fejléc (thead) - egy sor, ahol a harmadik cella 600 oszlopot von össze
  const thead = document.createElement('thead');
  const headerRow = document.createElement('tr');

  // "Day #" cella
  const thDay = document.createElement('th');
  thDay.textContent = 'Day #';
  headerRow.appendChild(thDay);

  // "Effective Load" cella
  const thLoad = document.createElement('th');
  thLoad.textContent = 'Effective Load';
  headerRow.appendChild(thLoad);

  // "Tasks" cella, 600 oszlop összevonásával
  const thTasks = document.createElement('th');
  thTasks.textContent = 'Tasks';
  thTasks.setAttribute('colspan', dayCapacity);
  thTasks.classList.add('text-center');
  headerRow.appendChild(thTasks);

  thead.appendChild(headerRow);
  table.appendChild(thead);

  // Tbody
  const tbody = document.createElement('tbody');

  days.forEach(day => {
    // Nap sora
    const dayRow = document.createElement('tr');

    // 1. cella: "Day X"
    const tdDayNum = document.createElement('td');
    tdDayNum.textContent = `Day ${day.dayNumber}`;
    tdDayNum.style.setProperty("background-color", "var(--bs-success-bg-subtle)");
    tdDayNum.style.setProperty("color", "var(--bs-success-text-emphasis)");

    dayRow.appendChild(tdDayNum);

    // 2. cella: "XYZ min"
    const tdLoad = document.createElement('td');
    tdLoad.textContent = `${day.effectiveLoad} min`;
    tdLoad.style.setProperty("background-color", "var(--bs-success-bg-subtle)");
    tdLoad.style.setProperty("color", "var(--bs-success-text-emphasis)");

    dayRow.appendChild(tdLoad);

    // Most jön a 600 "percoszlop", amit cellaösszevonással töltünk fel
    let columnsLeft = dayCapacity; // 600
    const assignments = day.assignments.filter(a => a.minutes > 0);

    assignments.forEach((assignment, index) => {
      if (columnsLeft <= 0) return;

      // Szín hozzárendelése, ha még nincs
      if (!taskColors[assignment.taskId]) {
        taskColors[assignment.taskId] = generateRandomColor();
      }
      const bgColor = taskColors[assignment.taskId];
      const textColor = getContrastColor(bgColor);

      // Feladat által igényelt percek
      let needed = assignment.minutes;
      if (needed > columnsLeft) {
        needed = columnsLeft; // ne lógjon túl a 600-ból
      }

      // Létrehozunk egy <td> colspan="needed"
      const tdTask = document.createElement('td');
      tdTask.classList.add('task');
      tdTask.setAttribute('colspan', needed);
      tdTask.style.backgroundColor = bgColor;
      tdTask.style.color = textColor;
      tdTask.textContent = assignment.taskName ;
      tdTask.dataset.taskId = assignment.taskId;

      // Hover események
      tdTask.addEventListener('mouseenter', () => {
        highlightTasks(assignment.taskId, true);
      });
      tdTask.addEventListener('mouseleave', () => {
        highlightTasks(assignment.taskId, false);
      });

      dayRow.appendChild(tdTask);
      columnsLeft -= needed;

      // Ha van még assignment utána, szúrjuk be a breakTime-ot
      if (index < assignments.length - 1 && columnsLeft > 0) {
        let breakNeeded = breakTime;
        if (breakNeeded > columnsLeft) {
          breakNeeded = columnsLeft;
        }
        const tdBreak = document.createElement('td');
        tdBreak.classList.add('break');
        tdBreak.setAttribute('colspan', breakNeeded);

        const icon = document.createElement('i');
        icon.classList.add('bi', 'bi-cup-hot-fill');
        tdBreak.appendChild(icon);

        dayRow.appendChild(tdBreak);
        columnsLeft -= breakNeeded;
      }
    });

    // Ha maradt még üres hely (columnsLeft > 0), kitöltjük egy üres cellával
    if (columnsLeft > 0) {
      const tdEmpty = document.createElement('td');
      tdEmpty.setAttribute('colspan', columnsLeft);
      tdEmpty.style.setProperty("background-color", "var(--bs-success-bg-subtle)");
      tdEmpty.style.setProperty("color", "var(--bs-success-text-emphasis)");

      const emptyfill=document.createElement('i')
      emptyfill.classList.add('bi','bi-ban')
      tdEmpty.appendChild(emptyfill)
      // üres marad
      dayRow.appendChild(tdEmpty);
      columnsLeft = 0;
    }

    tbody.appendChild(dayRow);
  });

  table.appendChild(tbody);
  container.appendChild(table);
}

// Feladat létrehozása
function createTask() {
  const taskname = document.getElementById('create-task-name').value;
  const totalhours = Number(document.getElementById('create-task-totalhours').value);
  const availabledays = Number(document.getElementById('create-task-availabledays').value);

  fetch('http://localhost:5267/scheduler', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: taskname,
      totalHours: totalhours,
      availableDays: availabledays
    })
  })
  .then(resp => {
    if (resp.status === 200) {
      downloadAndDisplay();
    } else {
      resp.json().then(err => {
        alert(err.message || 'Error creating task');
      });
    }
  })
  .catch(error => console.error(error));
}

// Szín generálás
function generateRandomColor() {
  return '#' + Math.floor(Math.random() * 16777215).toString(16);
}

// Szövegszín (fekete vagy fehér) a háttér kontrasztja alapján
function getContrastColor(hex) {
  hex = hex.replace('#', '');
  if (hex.length === 3) {
    hex = hex.split('').map(c => c + c).join('');
  }
  const r = parseInt(hex.substring(0, 2), 16);
  const g = parseInt(hex.substring(2, 4), 16);
  const b = parseInt(hex.substring(4, 6), 16);
  const brightness = (r * 299 + g * 587 + b * 114) / 1000;
  return brightness > 128 ? '#000000' : '#ffffff';
}

// Hover kiemelés az összes azonos taskId-jű cellán
function highlightTasks(taskId, highlight) {
  const allTasks = document.querySelectorAll('.task');
  allTasks.forEach(task => {
    if (task.dataset.taskId === taskId) {
      if (highlight) {
        task.classList.add('highlighted');
      } else {
        task.classList.remove('highlighted');
      }
    }
  });
}

// Indító hívás
downloadAndDisplay();
